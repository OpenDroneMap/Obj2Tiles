using System.Diagnostics;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Stages.Model;
using Obj2Tiles.Tiles;
using SilentWave.Obj2Gltf;

namespace Obj2Tiles;

public static class Utils
{
    public static IEnumerable<string> GetObjDependencies(string objPath)
    {
        var objFile = File.ReadAllLines(objPath);

        var dependencies = new List<string>();

        var folderName = Path.GetDirectoryName(objPath);

        foreach (var line in objFile)
        {
            var trimmedLine = line.Trim();

            if (!trimmedLine.StartsWith("mtllib")) continue;

            var mtlPath = Path.Combine(folderName, trimmedLine[7..].Trim());
            dependencies.Add(trimmedLine[7..].Trim());

            dependencies.AddRange(GetMtlDependencies(mtlPath));
        }

        return dependencies;
    }

    private static IList<string> GetMtlDependencies(string mtlPath)
    {
        var mtlFile = File.ReadAllLines(mtlPath);

        var dependencies = new List<string>();
        
        foreach (var line in mtlFile)
        {
            
            var trimmedLine = line.Trim();
            
            if (trimmedLine.StartsWith("map_Kd"))
            {
                dependencies.Add(trimmedLine[7..].Trim());

                continue;
            }

            if (trimmedLine.StartsWith("map_Ka"))
            {
                dependencies.Add(trimmedLine[7..].Trim());

                continue;
            }

            if (trimmedLine.StartsWith("norm"))
            {
                dependencies.Add(trimmedLine[5..].Trim());

                continue;
            }

            if (trimmedLine.StartsWith("map_Ks"))
            {
                dependencies.Add(trimmedLine[7..].Trim());

                continue;
            }

            if (trimmedLine.StartsWith("map_Bump"))
            {
                dependencies.Add(trimmedLine[8..].Trim());

                continue;
            }

            if (trimmedLine.StartsWith("map_d"))
            {
                dependencies.Add(trimmedLine[6..].Trim());

                continue;
            }

            if (trimmedLine.StartsWith("map_Ns"))
            {
                dependencies.Add(trimmedLine[7..].Trim());

                continue;
            }

            if (trimmedLine.StartsWith("bump"))
            {
                dependencies.Add(trimmedLine[5..].Trim());

                continue;
            }

            if (trimmedLine.StartsWith("disp"))
            {
                dependencies.Add(trimmedLine[5..].Trim());

                continue;
            }

            if (trimmedLine.StartsWith("decal"))
            {
                dependencies.Add(trimmedLine[6..].Trim());

                continue;
            }
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
        var dependencies = GetObjDependencies(input);

        foreach (var dependency in dependencies)
        {
            if (Path.IsPathRooted(dependency))
            {
                Debug.WriteLine(" ?> Cannot copy dependency because the path is rooted");
                continue;
            }

            var dependencyDestPath = Path.Combine(output, dependency);

            var destFolder = Path.GetDirectoryName(dependencyDestPath);
            if (destFolder != null) Directory.CreateDirectory(destFolder);

            if (File.Exists(dependencyDestPath))
                continue;

            var directoryName = Path.GetDirectoryName(input) ?? string.Empty;

            File.Copy(Path.Combine(directoryName, dependency), dependencyDestPath, true);

            Console.WriteLine($" -> Copied {dependency}");
        }
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
