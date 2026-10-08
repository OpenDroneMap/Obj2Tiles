using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Shouldly;

namespace Obj2Tiles.Test.Quality;

/// <summary>Inspector rules exercised on synthetic tilesets (no CLI, no Node).</summary>
[TestFixture]
public class TilesetInspectorTests
{
    // The sniffer only needs the SOI marker and at least 12 bytes.
    private static readonly byte[] JpegStub = { 0xff, 0xd8, 0xff, 0xe0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    private static byte[] Align4(byte[] data, byte pad)
    {
        var padded = (data.Length + 3) / 4 * 4;
        var result = Enumerable.Repeat(pad, padded).ToArray();
        data.CopyTo(result, 0);
        return result;
    }

    private static byte[] B3dmWithGlb(JObject gltf, byte[] bin)
    {
        var json = Align4(Encoding.UTF8.GetBytes(gltf.ToString(Newtonsoft.Json.Formatting.None)), 0x20);
        var binChunk = Align4(bin, 0);

        using var glb = new MemoryStream();
        var writer = new BinaryWriter(glb);
        writer.Write(0x46546C67u);
        writer.Write(2u);
        writer.Write((uint)(12 + 8 + json.Length + 8 + binChunk.Length));
        writer.Write((uint)json.Length);
        writer.Write(0x4E4F534Au);
        writer.Write(json);
        writer.Write((uint)binChunk.Length);
        writer.Write(0x004E4942u);
        writer.Write(binChunk);
        var glbBytes = glb.ToArray();

        using var b3dm = new MemoryStream();
        writer = new BinaryWriter(b3dm);
        writer.Write(Encoding.ASCII.GetBytes("b3dm"));
        writer.Write(1u);
        writer.Write((uint)(28 + glbBytes.Length));
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(glbBytes);
        return b3dm.ToArray();
    }

    // One JPEG image and the given primitive material indices; materials[0] is the unreferenced "default".
    private static string WriteTileset(string name, params int[] primitiveMaterials)
    {
        var gltf = new JObject
        {
            ["asset"] = new JObject { ["version"] = "2.0" },
            ["images"] = new JArray(new JObject { ["bufferView"] = 0 }),
            ["bufferViews"] = new JArray(new JObject { ["buffer"] = 0, ["byteOffset"] = 0, ["byteLength"] = JpegStub.Length }),
            ["buffers"] = new JArray(new JObject { ["byteLength"] = JpegStub.Length }),
            ["materials"] = new JArray(new JObject { ["name"] = "default" }, new JObject { ["name"] = "a" }, new JObject { ["name"] = "b" }),
            ["meshes"] = new JArray(new JObject
            {
                ["primitives"] = new JArray(primitiveMaterials.Select(m => new JObject { ["material"] = m })),
            }),
        };

        var dir = Path.Combine(CliHarness.TestOutputRoot, "inspector-" + name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(Path.Combine(dir, "LOD-0"));

        File.WriteAllBytes(Path.Combine(dir, "LOD-0", "Mesh.b3dm"), B3dmWithGlb(gltf, JpegStub));
        File.WriteAllText(Path.Combine(dir, "tileset.json"),
            """
            { "asset": { "version": "1.0" }, "geometricError": 100,
              "root": { "geometricError": 100, "refine": "REPLACE", "content": { "uri": "LOD-0/Mesh.b3dm" } } }
            """);
        return dir;
    }

    private static QualityExpectations Expect(Action<QualityExpectations>? configure = null)
    {
        var e = new QualityExpectations { Stage = Stage.Tiling, Lods = 1, Local = true };
        configure?.Invoke(e);
        return e;
    }

    [Test]
    public void EmbeddedImageOfWrongFormat_isReported()
    {
        var dir = WriteTileset("wrong-format", 1);

        var problems = TilesetInspector.Inspect(dir, Expect(e => e.TextureFormat = QualityTextureFormat.Webp));

        problems.ShouldContain(p => p.Contains("embedded image is JPEG but --texture-format Webp requested"));
    }

    [Test]
    public void MatchingImageFormat_isClean()
    {
        var dir = WriteTileset("matching-format", 1);

        TilesetInspector.Inspect(dir, Expect()).ShouldBeEmpty();
    }

    [Test]
    public void SingleMaterialPerPart_ignoresUnreferencedDefaultMaterial()
    {
        var dir = WriteTileset("smp-ok", 1);

        TilesetInspector.Inspect(dir, Expect(e => e.SingleMaterialPerPart = true)).ShouldBeEmpty();
    }

    [Test]
    public void SingleMaterialPerPart_flagsTwoReferencedMaterials()
    {
        var dir = WriteTileset("smp-bad", 1, 2);

        var problems = TilesetInspector.Inspect(dir, Expect(e => e.SingleMaterialPerPart = true));

        problems.ShouldContain(p => p.Contains("2 materials although --single-material-per-part was set"));
    }
}
