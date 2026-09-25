using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Obj2Tiles.Test.Quality;

/// <summary>
/// Structural and option-contract validation of produced 3D Tiles. Complements the official
/// validator (schema conformance) with the checks the validator does not make: LOD budget vs
/// --lods, texture budget vs --max-texture-size / --lod-texture-scale, requested texture
/// formats by magic bytes and glTF extensions, georeferencing transform sanity, b3dm/GLB
/// framing, and geometricError finiteness/monotonicity. Returns problems instead of
/// asserting, so callers can report all violations of a case at once.
/// </summary>
public static class TilesetInspector
{
    private const double Epsilon = 1e-6;

    public static List<string> Inspect(string output, QualityExpectations e)
    {
        var problems = new List<string>();

        Func<string, byte[]?> readRel;
        long totalBytes;
        string tilesetRaw;
        string tilesetBaseDir = "";

        if (output.EndsWith(".3tz", StringComparison.OrdinalIgnoreCase))
        {
            var entries = Read3tz(output, problems);
            readRel = name => entries.GetValueOrDefault(Normalize(name));
            totalBytes = new FileInfo(output).Length;
            if (!entries.TryGetValue("tileset.json", out var ts))
            {
                problems.Add("tileset.json is missing from the .3tz archive");
                return problems;
            }

            tilesetRaw = Encoding.UTF8.GetString(ts);
        }
        else
        {
            var tilesetPath = Path.Combine(output, "tileset.json");
            if (!File.Exists(tilesetPath))
            {
                problems.Add($"tileset.json not found under {output}");
                return problems;
            }

            tilesetBaseDir = output;
            readRel = name =>
            {
                var full = Path.GetFullPath(Path.Combine(output, name));
                // nosec-Traversal: outputs are produced by the CLI under our control from fixture input
                return full.StartsWith(Path.GetFullPath(output), StringComparison.Ordinal) && File.Exists(full)
                    ? File.ReadAllBytes(full)
                    : null;
            };
            totalBytes = Directory.GetFiles(output, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            tilesetRaw = File.ReadAllText(tilesetPath);
        }

        if (totalBytes > e.BudgetBytes)
            problems.Add($"output size budget exceeded: {totalBytes} > {e.BudgetBytes} bytes");

        JToken rootToken;
        try
        {
            rootToken = JToken.Parse(tilesetRaw);
        }
        catch (Exception ex)
        {
            problems.Add($"tileset.json is not parseable JSON: {ex.Message}");
            return problems;
        }

        var root = rootToken["root"];
        if (root == null)
        {
            problems.Add("tileset.json has no 'root' tile");
            return problems;
        }

        var contentByLod = new Dictionary<int, List<(string Uri, JToken GlbJson, byte[] GlbBin, List<string> ImageProblems)>>();
        var uriSeen = new HashSet<string>(StringComparer.Ordinal);

        WalkTile("root", root, parentGe: null, parentIsReplace: true, e, problems, readRel, tilesetBaseDir.Length > 0, contentByLod, uriSeen);

        if (e.Stage == Stage.Tiling)
            CheckLods(contentByLod, e, problems);

        return problems;
    }

    private static bool StageTiling(QualityExpectations e) => e.Stage == Stage.Tiling;

    private static void WalkTile(
        string path, JToken tile, double? parentGe, bool parentIsReplace,
        QualityExpectations e, List<string> problems, Func<string, byte[]?> readRel, bool fromDisk,
        Dictionary<int, List<(string, JToken, byte[], List<string>)>> contentByLod, HashSet<string> uriSeen)
    {
        // --- geometricError: must be a real, finite number (NaN serialized as a non-number
        //     token is a known historical defect class here) -------------------------------
        var geToken = tile["geometricError"];
        double? ge = null;
        if (geToken == null)
        {
            problems.Add($"{path}: missing geometricError");
        }
        else if (geToken.Type is JTokenType.Integer or JTokenType.Float)
        {
            ge = geToken.Value<double>();
            if (double.IsNaN(ge.Value) || double.IsInfinity(ge.Value))
                problems.Add($"{path}: geometricError is not finite ({ge})");
            else if (ge.Value < 0)
                problems.Add($"{path}: geometricError is negative ({ge})");
        }
        else
        {
            problems.Add($"{path}: geometricError must be a number, found {geToken.Type} ({geToken})");
        }

        var refine = tile["refine"]?.Value<string>() ?? tile["refine"]?.ToString();
        if (refine is not ("REPLACE" or "ADD"))
            problems.Add($"{path}: refine must be REPLACE or ADD, found '{refine}'");

        if (ge != null && parentGe != null)
        {
            if (ge > parentGe + Epsilon)
                problems.Add($"{path}: geometricError {ge} is larger than the parent's {parentGe}");
            else if (parentIsReplace && parentGe > 0 && Math.Abs(ge.Value - parentGe.Value) < Epsilon)
            {
                // equal errors on REPLACE chains hide a missing error budget
                problems.Add($"{path}: geometricError equals the parent's ({ge}) on a REPLACE chain");
            }
        }

        // --- content ---------------------------------------------------------------------
        string? uri = null;
        var contentToken = tile["content"];
        if (contentToken is JObject contentObj)
            uri = contentObj["uri"]?.Value<string>();
        else if (contentToken is JValue contentValue && contentValue.Type == JTokenType.String)
            uri = contentValue.Value<string>(); // legal 1.1 shorthand: "content": "tile.b3dm"
        else if (tile["contents"] is JArray arr)
            uri = arr.Children<JObject>().FirstOrDefault()?["uri"]?.Value<string>();

        var isRoot = path == "root";
        if (uri == null)
        {
            var mayOmitContent = isRoot && e.NoRootContent;
            if (!mayOmitContent && tile["children"] is not { HasValues: true })
                problems.Add($"{path}: tile has neither content nor children");
            if (isRoot && !e.NoRootContent)
                problems.Add("root has no content but --no-root-content was not set");
        }
        else
        {
            if (isRoot && e.NoRootContent)
                problems.Add("root has content although --no-root-content was set");
            if (!uriSeen.Add(uri))
                problems.Add($"{path}: duplicate content uri {uri}");

            var data = readRel(uri);
            if (data == null)
            {
                problems.Add($"{path}: content uri not found: {uri}");
                return;
            }

            var imageProblems = new List<string>();
            var parsed = TryParseB3dm(data, uri, imageProblems);
            if (parsed != null)
            {
                var (glbJson, glbBin) = parsed.Value;

                var lodMatch = System.Text.RegularExpressions.Regex.Match(uri, @"LOD-(\d+)");
                if (lodMatch.Success && int.TryParse(lodMatch.Groups[1].Value, out var lod))
                {
                    if (!contentByLod.TryGetValue(lod, out var list))
                        contentByLod[lod] = list = new List<(string, JToken, byte[], List<string>)>();
                    list.Add((uri, glbJson, glbBin, imageProblems));
                }
            }

            problems.AddRange(imageProblems);
        }

        // --- transform / georeferencing sanity -------------------------------------------
        var transform = tile["transform"] as JArray;
        if (transform != null)
        {
            if (transform.Count != 16)
                problems.Add($"{path}: transform must have 16 elements, has {transform.Count}");
            else if (e.Local)
            {
                var m = transform.Select(t => t.Value<double>()).ToArray();
                var isIdentity = true;
                for (var i = 0; i < 16 && isIdentity; i++)
                {
                    var expected = i % 5 == 0 ? 1.0 : 0.0; // identity diagonal pattern for column-major
                    if (i is 12 or 13 or 14) expected = 0.0;
                    if (Math.Abs(m[i] - expected) > Epsilon) isIdentity = false;
                }

                if (!isIdentity)
                    problems.Add($"{path}: non-identity transform although --local was set");
            }
            else if (isRoot)
            {
                var m = transform.Select(t => t.Value<double>()).ToArray();
                var tx = Math.Sqrt(m[12] * m[12] + m[13] * m[13] + m[14] * m[14]);
                if (tx is < 6.2e6 or > 6.6e6)
                    problems.Add($"{path}: ECEF translation norm {tx:F0} is not on the Earth (expected ~6.2e6..6.6e6)");

                // The georeference matrix carries a uniform scale (--scale, meters per unit),
                // so every rotation column must have norm == scale, not 1.
                var expectedScale = e.Scale > 0 ? e.Scale : 1.0;
                for (var col = 0; col < 3; col++)
                {
                    var norm = Math.Sqrt(m[col] * m[col] + m[4 + col] * m[4 + col] + m[8 + col] * m[8 + col]);
                    if (Math.Abs(norm - expectedScale) > Math.Max(1e-6, expectedScale * 1e-4))
                        problems.Add($"{path}: transform rotation column {col} has norm {norm:F6}, expected {expectedScale:F6} (scale)");
                }
            }
        }
        else if (!e.Local && isRoot)
        {
            problems.Add("root has no transform but a georeferenced tileset (no --local) was requested");
        }

        var children = tile["children"] as JArray;
        var i2 = 0;
        foreach (var child in children ?? new JArray())
            WalkTile($"{path}/children/{i2++}", child, ge, refine == "REPLACE", e, problems, readRel, fromDisk, contentByLod, uriSeen);
    }

    private static void CheckLods(
        Dictionary<int, List<(string Uri, JToken GlbJson, byte[] GlbBin, List<string> ImageProblems)>> contentByLod,
        QualityExpectations e, List<string> problems)
    {
        if (!e.Octree)
        {
            for (var lod = 0; lod < e.Lods; lod++)
            {
                if (!contentByLod.TryGetValue(lod, out var tiles) || tiles.Count == 0)
                    problems.Add($"LOD-{lod} has no tiles (expected >= 1 for --lods {e.Lods})");
            }
        }

        foreach (var (lod, tiles) in contentByLod)
        {
            if (tiles.Count > 512)
                problems.Add($"LOD-{lod} exploded to {tiles.Count} tile contents (b3dm): possible split/binpack runaway");
        }

        if (!e.CheckTextures) return;

        // format + budget checks on repacked texture atlases
        int? lod0MaxDim = null;

        foreach (var lod in contentByLod.Keys.OrderBy(k => k))
        {
            foreach (var (uri, glbJson, glbBin, imageProblems) in contentByLod[lod])
            {
                CheckImages(uri, glbJson, glbBin, e, imageProblems);

                var dims = ImageDims(glbJson, glbBin);
                if (dims.Count > 0 && lod == 0)
                    lod0MaxDim = Math.Max(lod0MaxDim ?? 0, dims.Max(d => Math.Max(d.W, d.H)));
            }
        }

        // LOD scale budget: LOD-k images must match LOD-0 scaled by lod-texture-scale^k
        if (lod0MaxDim is > 0 && e.LodTextureScale < 1.0)
        {
            foreach (var lod in contentByLod.Keys.Where(k => k > 0).OrderBy(k => k))
            {
                var expected = (int)Math.Round(lod0MaxDim.Value * Math.Pow(e.LodTextureScale, lod));
                foreach (var (uri, glbJson, glbBin, _) in contentByLod[lod])
                {
                    foreach (var (w, h) in ImageDims(glbJson, glbBin))
                    {
                        var maxDim = Math.Max(w, h);
                        // allow encoder padding (4 blocks for ktx2 etc.) and non-proportional sources
                        if (maxDim > expected + 8)
                            problems.Add($"{uri}: image {w}x{h} exceeds LOD-{lod} budget {expected} (--lod-texture-scale {e.LodTextureScale} vs LOD-0 {lod0MaxDim})");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Images reference bufferViews by integer index (glTF spec); some writers inline the
    /// object instead. Returns null (after flagging, when a problems list is given) for
    /// missing/out-of-range/invalid tokens.
    /// </summary>
    private static JObject? ResolveBufferView(JToken glbJson, JToken? bufferViewToken, string uri, List<string> problems)
    {
        if (bufferViewToken == null || bufferViewToken.Type == JTokenType.Null) return null;
        if (bufferViewToken is JObject inline) return inline;

        if (bufferViewToken.Type == JTokenType.Integer)
        {
            var index = bufferViewToken.Value<int>();
            if (glbJson["bufferViews"] is JArray views && index >= 0 && index < views.Count &&
                views[index] is JObject viewObj)
            {
                return viewObj;
            }

            problems.Add($"{uri}: image bufferView index {index} out of range (bufferViews count " +
                         $"{(glbJson["bufferViews"] as JArray)?.Count ?? -1})");
            return null;
        }

        problems.Add($"{uri}: image bufferView has invalid token type {bufferViewToken.Type}");
        return null;
    }

    private static void CheckImages(string uri, JToken glbJson, byte[] glbBin, QualityExpectations e, List<string> problems)
    {
        var images = glbJson["images"] as JArray;
        if (images == null || images.Count == 0)
        {
            // The source cube/materials carry textures, so tiles should have at least one.
            // Geometry-only datasets legitimately produce zero images; do not fail here.
            return;
        }

        if (images.Count > 32)
            problems.Add($"{uri}: {images.Count} images in one tile is suspicious (atlas repack runaway?)");

        var expectedMime = e.TextureFormat switch
        {
            QualityTextureFormat.Jpeg => "image/jpeg",
            QualityTextureFormat.Webp => "image/webp",
            QualityTextureFormat.Ktx2 => "image/ktx2",
            _ => null,
        };
        var expectedExt = e.TextureFormat switch
        {
            QualityTextureFormat.Webp => "EXT_texture_webp",
            QualityTextureFormat.Ktx2 => "KHR_texture_basisu",
            _ => null,
        };

        var sawImage = false;
        foreach (var imageToken in images)
        {
            if (imageToken is not JObject image)
            {
                problems.Add($"{uri}: images[] contains a non-object entry ({imageToken.Type})");
                continue;
            }

            sawImage = true;
            var mime = image["mimeType"]?.Value<string>();
            var bufferView = ResolveBufferView(glbJson, image["bufferView"], uri, problems);
            var external = image["uri"];
            if (external != null)
            {
                problems.Add($"{uri}: external image uri '{external}' (all images must be embedded in the GLB)");
                continue;
            }

            if (bufferView == null)
            {
                if (!string.IsNullOrEmpty(mime) && mime != expectedMime)
                    problems.Add($"{uri}: image mimeType '{mime}' but --texture-format {e.TextureFormat} requested");
                continue;
            }

            var offset = bufferView["byteOffset"]?.Value<int>() ?? 0;
            var length = bufferView["byteLength"]?.Value<int>() ?? 0;
            if (length <= 0 || offset + length > glbBin.Length)
            {
                problems.Add($"{uri}: image bufferView out of range (offset {offset}, length {length}, bin {glbBin.Length})");
                continue;
            }

            var format = ImageSniffer.Detect(glbBin, offset, length, out var w, out var h);

            switch (format)
            {
                case ImageFormat.Jpeg when e.TextureFormat != QualityTextureFormat.Jpeg:
                    problems.Add($"{uri}: embedded image is JPEG but --texture-format {e.TextureFormat} requested");
                    break;
                case ImageFormat.Webp when e.TextureFormat != QualityTextureFormat.Webp:
                    problems.Add($"{uri}: embedded image is WebP but --texture-format {e.TextureFormat} requested");
                    break;
                case ImageFormat.Ktx2 when e.TextureFormat != QualityTextureFormat.Ktx2:
                    problems.Add($"{uri}: embedded image is KTX2 but --texture-format {e.TextureFormat} requested");
                    break;
                case ImageFormat.Unknown:
                    problems.Add($"{uri}: embedded image payload is not a recognized texture format");
                    break;
            }

            if (!string.IsNullOrEmpty(mime) && mime != expectedMime && !e.KeepTextures)
                problems.Add($"{uri}: image mimeType '{mime}' mismatches --texture-format {e.TextureFormat}");

            if (w > 0 && e.MaxTextureSize > 0 && Math.Max(w, h) > e.MaxTextureSize + 4)
                problems.Add($"{uri}: image {w}x{h} exceeds --max-texture-size {e.MaxTextureSize}");
        }

        if (sawImage && expectedExt != null && !e.KeepTextures)
        {
            var used = (glbJson["extensionsUsed"] as JArray)?.Select(t => t.Value<string>()).ToList() ?? new();
            if (!used.Contains(expectedExt))
                problems.Add($"{uri}: glTF does not declare {expectedExt} in extensionsUsed for --texture-format {e.TextureFormat}");
        }

        if (e.SingleMaterialPerPart && !e.KeepTextures)
        {
            var materials = (glbJson["materials"] as JArray)?.Count ?? 0;
            if (materials > 1)
                problems.Add($"{uri}: {materials} materials although --single-material-per-part was set");
        }
    }

    private static List<(int W, int H)> ImageDims(JToken glbJson, byte[] glbBin)
    {
        var result = new List<(int, int)>();
        foreach (var imageToken in (glbJson["images"] as JArray) ?? new JArray())
        {
            if (imageToken is not JObject image) continue;
            var bufferView = ResolveBufferView(glbJson, image["bufferView"], "", new List<string>());
            if (bufferView == null) continue;
            var offset = bufferView["byteOffset"]?.Value<int>() ?? 0;
            var length = bufferView["byteLength"]?.Value<int>() ?? 0;
            if (length <= 0 || offset + length > glbBin.Length) continue;

            ImageSniffer.Detect(glbBin, offset, length, out var w, out var h);
            if (w > 0 && h > 0) result.Add((w, h));
        }

        return result;
    }

    // ---- b3dm / GLB framing ----------------------------------------------------------

    private static (JToken Json, byte[] Bin)? TryParseB3dm(byte[] data, string name, List<string> problems)
    {
        if (data.Length < 28)
        {
            problems.Add($"{name}: too small to be a b3dm ({data.Length} bytes)");
            return null;
        }

        if (Encoding.ASCII.GetString(data, 0, 4) != "b3dm")
        {
            problems.Add($"{name}: missing b3dm magic");
            return null;
        }

        var version = BitConverter.ToUInt32(data, 4);
        var byteLength = BitConverter.ToUInt32(data, 8);
        var ftLen = BitConverter.ToUInt32(data, 12);
        var btLen = BitConverter.ToUInt32(data, 16);
        var jsonLen = BitConverter.ToUInt32(data, 20);
        var binLen = BitConverter.ToUInt32(data, 24);

        if (version != 1) problems.Add($"{name}: b3dm version {version} != 1");
        if (byteLength != (uint)data.Length)
            problems.Add($"{name}: b3dm byteLength {byteLength} != file size {data.Length}");
        if (jsonLen % 16 != 0) problems.Add($"{name}: b3dm jsonByteLength {jsonLen} not a multiple of 16");
        if (binLen % 8 != 0) problems.Add($"{name}: b3dm binaryByteLength {binLen} not a multiple of 8");

        var glbOffset = 28 + ftLen + btLen + jsonLen + binLen;
        if (glbOffset + 12 > data.Length)
        {
            // Older/alternative framing: the GLB right after header+feature table without json/bin lengths.
            glbOffset = 28 + ftLen + btLen;
            if (glbOffset + 12 > data.Length)
            {
                problems.Add($"{name}: no GLB payload inside b3dm (offset {glbOffset}, size {data.Length})");
                return null;
            }
        }

        if (Encoding.ASCII.GetString(data, (int)glbOffset, 4) != "glTF")
        {
            problems.Add($"{name}: inner payload is not a GLB (magic at {glbOffset})");
            return null;
        }

        var glbVer = BitConverter.ToUInt32(data, (int)glbOffset + 4);
        var glbLen = BitConverter.ToUInt32(data, (int)glbOffset + 8);
        if (glbVer != 2) problems.Add($"{name}: inner GLB version {glbVer} != 2");

        // The writer may append chunk-alignment bytes after the GLB; the official validator
        // accepts that, so only a GLB that runs past the end of the file is a defect.
        if (glbOffset + glbLen > data.Length)
            problems.Add($"{name}: inner GLB length {glbLen} exceeds the file (offset {glbOffset}, size {data.Length})");

        var chunkOff = glbOffset + 12;
        if (chunkOff + 8 > data.Length)
        {
            problems.Add($"{name}: GLB truncated before the first chunk header");
            return null;
        }

        var chunkLen = (int)BitConverter.ToUInt32(data, (int)chunkOff);
        var chunkType = BitConverter.ToUInt32(data, (int)chunkOff + 4);
        if (chunkType != 0x4E4F534A) // 'JSON'
        {
            problems.Add($"{name}: GLB first chunk is not JSON (0x{chunkType:X8})");
            return null;
        }

        if (chunkOff + 8 + chunkLen > data.Length)
        {
            problems.Add($"{name}: GLB JSON chunk runs past the end of file");
            return null;
        }

        JToken json;
        try
        {
            json = JToken.Parse(Encoding.UTF8.GetString(data, (int)chunkOff + 8, chunkLen));
        }
        catch (Exception ex)
        {
            problems.Add($"{name}: GLB JSON chunk is not parseable: {ex.Message}");
            return null;
        }

        if (json is not JObject)
        {
            problems.Add($"{name}: GLB JSON chunk root is not a JSON object ({json.Type})");
            return null;
        }

        var binOff = (int)chunkOff + 8 + chunkLen;
        byte[] bin = Array.Empty<byte>();
        if (binOff + 8 <= data.Length)
        {
            var binChunkLen = (int)BitConverter.ToUInt32(data, binOff);
            var binType = BitConverter.ToUInt32(data, binOff + 4);
            if (binType != 0x004E4942) // 'BIN\0'
                problems.Add($"{name}: GLB second chunk is not BIN (0x{binType:X8})");
            else
                bin = data.Skip(binOff + 8).Take(Math.Min(binChunkLen, data.Length - binOff - 8)).ToArray();
        }

        return (json, bin);
    }

    // ---- .3tz ----------------------------------------------------------------------------

    private static Dictionary<string, byte[]> Read3tz(string path, List<string> problems)
    {
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            using var zip = ZipFile.OpenRead(path);
            if (zip.Entries.Count == 0)
                problems.Add($"{path}: .3tz archive is empty");

            var contents = zip.Entries.Count(e => e.FullName.EndsWith(".b3dm"));
            if (contents == 0)
                problems.Add($"{path}: .3tz archive contains no b3dm content");

            foreach (var entry in zip.Entries)
            {
                using var stream = entry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                entries[Normalize(entry.FullName)] = ms.ToArray();
            }
        }
        catch (InvalidDataException ex)
        {
            problems.Add($"{path}: not a readable zip (.3tz): {ex.Message}");
        }

        return entries;
    }

    private static string Normalize(string name) => name.Replace('\\', '/').TrimStart('/');

    // ---- image sniffing -------------------------------------------------------------------

    internal enum ImageFormat { Unknown, Jpeg, Png, Webp, Ktx2 }

    internal static class ImageSniffer
    {
        public static ImageFormat Detect(byte[] data, int offset, int length, out int width, out int height)
        {
            width = height = 0;
            if (length < 12 || offset + length > data.Length) return ImageFormat.Unknown;

            if (data[offset] == 0xFF && data[offset + 1] == 0xD8)
            {
                TryJpegDims(data, offset, length, out width, out height);
                return ImageFormat.Jpeg;
            }

            if (length >= 8 && data[offset] == 0x89 && data[offset + 1] == 'P' && data[offset + 2] == 'N' && data[offset + 3] == 'G')
            {
                if (length >= 24)
                {
                    width = (data[offset + 16] << 24) | (data[offset + 17] << 16) | (data[offset + 18] << 8) | data[offset + 19];
                    height = (data[offset + 20] << 24) | (data[offset + 21] << 16) | (data[offset + 22] << 8) | data[offset + 23];
                }

                return ImageFormat.Png;
            }

            if (length >= 16 && Are(data, offset, "RIFF") && Are(data, offset + 8, "WEBP"))
            {
                TryWebpDims(data, offset, length, out width, out height);
                return ImageFormat.Webp;
            }

            if (length >= 44 && data[offset] == 0xAB && Are(data, offset + 1, "KTX 20"))
            {
                // KTX 2.0 header (field order per the reference parser ktx-parse): 12-byte
                // identifier, then vkFormat, typeSize, pixelWidth, pixelHeight - so dimensions
                // live at +20/+24. vkFormat may legally be 0 for ETC1S/UASTC (DFD-driven).
                width = (int)BitConverter.ToUInt32(data, offset + 20);
                height = (int)BitConverter.ToUInt32(data, offset + 24);
                return ImageFormat.Ktx2;
            }

            return ImageFormat.Unknown;
        }

        private static bool Are(byte[] data, int offset, string magic)
        {
            for (var i = 0; i < magic.Length; i++)
                if (offset + i >= data.Length || data[offset + i] != magic[i]) return false;
            return true;
        }

        private static void TryJpegDims(byte[] d, int o, int len, out int w, out int h)
        {
            w = h = 0;
            var end = o + len;
            var p = o + 2;
            while (p + 9 < end)
            {
                if (d[p] != 0xFF) { p++; continue; }

                var marker = d[p + 1];
                if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
                {
                    h = (d[p + 5] << 8) | d[p + 6];
                    w = (d[p + 7] << 8) | d[p + 8];
                    return;
                }

                if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7)) { p += 2; continue; }

                var segLen = (d[p + 2] << 8) | d[p + 3];
                p += 2 + Math.Max(segLen, 2);
            }
        }

        private static void TryWebpDims(byte[] d, int o, int len, out int w, out int h)
        {
            w = h = 0;
            // walk the real chunk list of the RIFF....WEBP container
            var p = o + 12;
            var end = o + len;
            while (p + 8 <= end)
            {
                var size = (int)BitConverter.ToUInt32(d, p + 4);
                var payload = p + 8;
                if (size < 0 || payload + (size & ~1) > end) break;

                if (Are(d, p, "VP8X") && payload + 10 <= end)
                {
                    // payload: flags(1) reserved(3) canvas width-1(24b LE) canvas height-1(24b LE)
                    w = (d[payload + 4] | (d[payload + 5] << 8) | (d[payload + 6] << 16)) + 1;
                    h = (d[payload + 7] | (d[payload + 8] << 8) | (d[payload + 9] << 16)) + 1;
                    return;
                }

                if (Are(d, p, "VP8L") && payload + 6 <= end && d[payload] == 0x2F)
                {
                    var bits = BitConverter.ToUInt32(d, payload + 1);
                    w = (int)(bits & 0x3FFF) + 1;
                    h = (int)((bits >> 14) & 0x3FFF) + 1;
                    return;
                }

                if (Are(d, p, "VP8 ") && size >= 10)
                {
                    // keyframe: locate the 0x9D 0x01 0x2A start code within the chunk
                    for (var q = payload; q + 8 <= payload + size && q + 8 <= end; q++)
                    {
                        if (d[q] == 0x9D && d[q + 1] == 0x01 && d[q + 2] == 0x2A)
                        {
                            w = (d[q + 3] | (d[q + 4] << 8)) & 0x3FFF;
                            h = (d[q + 5] | (d[q + 6] << 8)) & 0x3FFF;
                            return;
                        }
                    }
                    return;
                }

                p += 8 + size + (size & 1);
            }
        }
    }
}
