using System.Diagnostics;
using System.Globalization;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Stages.Model;
using Obj2Tiles.Tiles;
using SilentWave.Obj2Gltf;

namespace Obj2Tiles;

public static class Utils
{
    private static readonly string[] MtlMapKeywords =
    {
        "map_Kd", "map_Ka", "map_Ks", "map_Bump", "map_d", "map_Ns",
        "norm", "bump", "disp", "decal"
    };

    private static readonly Dictionary<string, int> MtlOptionValueCounts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["-blendu"] = 1, ["-blendv"] = 1, ["-bm"] = 1, ["-boost"] = 1,
            ["-cc"] = 1, ["-clamp"] = 1, ["-imfchan"] = 1, ["-mm"] = 2,
            ["-texres"] = 1, ["-type"] = 1,
            // -o, -s, -t take 1-3 numeric values and are handled separately
        };

    // Strips leading MTL option arguments (e.g. "-bm 1.0 -s 1 1") and returns
    // the texture path with separators normalized for the current platform.
    private static string ExtractTexturePath(string lineRemainder)
    {
        var tokens = lineRemainder.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int i = 0;
        while (i < tokens.Length && tokens[i].StartsWith('-'))
        {
            var option = tokens[i];
            i++;
            if (option.Equals("-o", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("-t", StringComparison.OrdinalIgnoreCase))
            {
                int consumed = 0;
                while (i < tokens.Length && consumed < 3 &&
                       double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                { i++; consumed++; }
            }
            else
            {
                MtlOptionValueCounts.TryGetValue(option, out var cnt);
                if (cnt == 0) cnt = 1;
                i = Math.Min(i + cnt, tokens.Length);
            }
        }
        var raw = string.Join(' ', tokens[i..]);
        return raw.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    // Tries to locate a file by progressively relaxing the base directory.
    // Returns the resolved absolute path, or null if no candidate exists.
    private static string? ResolveTexturePath(string path, string mtlFolder, string objFolder)
    {
        string candidate;

        candidate = Path.GetFullPath(Path.Combine(mtlFolder, path));
        if (File.Exists(candidate)) return candidate;

        if (!string.Equals(mtlFolder, objFolder, StringComparison.OrdinalIgnoreCase))
        {
            candidate = Path.GetFullPath(Path.Combine(objFolder, path));
            if (File.Exists(candidate)) return candidate;
        }

        var fileName = Path.GetFileName(path);
        if (fileName.Length < path.Length)
        {
            candidate = Path.GetFullPath(Path.Combine(mtlFolder, fileName));
            if (File.Exists(candidate)) return candidate;

            if (!string.Equals(mtlFolder, objFolder, StringComparison.OrdinalIgnoreCase))
            {
                candidate = Path.GetFullPath(Path.Combine(objFolder, fileName));
                if (File.Exists(candidate)) return candidate;
            }
        }

        candidate = Path.GetFullPath(path);
        if (File.Exists(candidate)) return candidate;

        return null;
    }

    // Returns (normalizedRelPath, resolvedAbsPath) pairs for every texture
    // referenced by an MTL file. Uses the full resolution algorithm.
    private static IEnumerable<(string relPath, string resolvedPath)> GetMtlTextureDependencies(
        string mtlPath, string objFolder)
    {
        if (!File.Exists(mtlPath)) yield break;

        var mtlFolder = Path.GetDirectoryName(Path.GetFullPath(mtlPath)) ?? string.Empty;

        foreach (var line in File.ReadAllLines(mtlPath))
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#')) continue;

            foreach (var keyword in MtlMapKeywords)
            {
                if (!trimmedLine.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)) continue;
                // Reject prefix matches like "map_Kd2" for "map_Kd"
                if (trimmedLine.Length > keyword.Length &&
                    trimmedLine[keyword.Length] != ' ' && trimmedLine[keyword.Length] != '\t') continue;

                var remainder = trimmedLine.Length > keyword.Length
                    ? trimmedLine[(keyword.Length + 1)..]
                    : string.Empty;

                var texPath = ExtractTexturePath(remainder);
                if (string.IsNullOrWhiteSpace(texPath)) break;

                if (Path.IsPathRooted(texPath))
                {
                    Debug.WriteLine($" ?> Skipping rooted MTL dependency: {texPath}");
                    break;
                }

                var resolved = ResolveTexturePath(texPath, mtlFolder, objFolder);
                if (resolved != null)
                    yield return (texPath, resolved);
                else
                    Debug.WriteLine($" ?> Could not resolve MTL dependency: {texPath}");

                break;
            }
        }
    }

    // Returns the normalized relative paths of all files that the OBJ depends on
    // (the MTL itself plus every texture it references).
    public static IEnumerable<string> GetObjDependencies(string objPath)
    {
        var objFolder = Path.GetDirectoryName(Path.GetFullPath(objPath)) ?? string.Empty;
        var dependencies = new List<string>();

        foreach (var line in File.ReadAllLines(objPath))
        {
            var trimmedLine = line.Trim();
            if (!trimmedLine.StartsWith("mtllib")) continue;
            if (trimmedLine.Length <= 6 || (trimmedLine[6] != ' ' && trimmedLine[6] != '\t')) continue;

            var mtlRelPath = trimmedLine[7..].Trim()
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            dependencies.Add(mtlRelPath);

            var mtlAbsPath = Path.Combine(objFolder, mtlRelPath);
            foreach (var (relPath, _) in GetMtlTextureDependencies(mtlAbsPath, objFolder))
                dependencies.Add(relPath);
        }

        return dependencies;
    }


    public static BoundingVolume ToBoundingVolume(this Box3 box)
    {
        return new BoundingVolume
        {
            Box = [
                box.Center.X, -box.Center.Z, box.Center.Y,
                box.Width / 2, 0, 0,
                0, -box.Depth / 2, 0,
                0, 0, box.Height / 2
            ]
        };
    }
    
    public static void CopyObjDependencies(string input, string output)
    {
        var objFolder = Path.GetDirectoryName(Path.GetFullPath(input)) ?? string.Empty;
        var outputFull = Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var line in File.ReadAllLines(input))
        {
            var trimmedLine = line.Trim();
            if (!trimmedLine.StartsWith("mtllib")) continue;
            if (trimmedLine.Length <= 6 || (trimmedLine[6] != ' ' && trimmedLine[6] != '\t')) continue;

            var mtlRelPath = trimmedLine[7..].Trim()
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            var mtlSrcPath = Path.Combine(objFolder, mtlRelPath);
            var mtlDestPath = Path.Combine(output, mtlRelPath);
            if (!IsWithinOutput(outputFull, mtlDestPath))
            {
                Debug.WriteLine($" ?> Skipping MTL outside output folder: {mtlRelPath}");
                continue;
            }
            var mtlDestFolder = Path.GetDirectoryName(mtlDestPath);
            if (mtlDestFolder != null) Directory.CreateDirectory(mtlDestFolder);

            if (!File.Exists(mtlDestPath) && File.Exists(mtlSrcPath))
            {
                File.Copy(mtlSrcPath, mtlDestPath, true);
                Console.WriteLine($" -> Copied {mtlRelPath}");
            }

            foreach (var (relPath, absPath) in GetMtlTextureDependencies(mtlSrcPath, objFolder))
            {
                var texDestPath = Path.Combine(output, relPath);
                if (!IsWithinOutput(outputFull, texDestPath))
                {
                    Debug.WriteLine($" ?> Skipping texture outside output folder: {relPath}");
                    continue;
                }
                var texDestFolder = Path.GetDirectoryName(texDestPath);
                if (texDestFolder != null) Directory.CreateDirectory(texDestFolder);

                if (File.Exists(texDestPath)) continue;

                File.Copy(absPath, texDestPath, true);
                Console.WriteLine($" -> Copied {relPath}");
            }
        }
    }
    
    // Guards against path traversal: ensures destPath resolves inside the output folder.
    private static bool IsWithinOutput(string outputFull, string destPath)
    {
        var full = Path.GetFullPath(destPath);
        return full == outputFull ||
               full.StartsWith(outputFull + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    public static void ConvertB3dm(string objPath, string destPath)
    {
        var dir = Path.GetDirectoryName(objPath);
        var name = Path.GetFileNameWithoutExtension(objPath);

        var converter = Converter.MakeDefault();
        var outputFile = dir != null ? Path.Combine(dir, $"{name}.gltf") : $"{name}.gltf";

        converter.Convert(objPath, outputFile);

        var glbConv = new Gltf2GlbConverter();
        glbConv.Convert(new Gltf2GlbOptions(outputFile));

        var glbFile = Path.ChangeExtension(outputFile, ".glb");

        var b3dm = new B3dm(File.ReadAllBytes(glbFile));

        File.WriteAllBytes(destPath, b3dm.ToBytes());
    }
}
