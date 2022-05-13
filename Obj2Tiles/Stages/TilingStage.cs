using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Stages.Model;
using Obj2Tiles.Tiles;
using SilentWave;
using SilentWave.Obj2Gltf;

namespace Obj2Tiles.Stages;

public static partial class StagesFacade
{
    public static void Tile(string sourcePath, string destPath, int lods, GpsCoords? coords = null)
    {
        coords ??= DefaultGpsCoords;

        ConvertAllB3dm(sourcePath, destPath, lods);

        const int baseError = 1024;

        var tileset = new Tileset
        {
            Asset = new Asset { Version = "1.0" },
            GeometricError = baseError,
            Root = new TileElement
            {
                GeometricError = baseError,
                Refine = "ADD",
                Transform = new List<double>
                {
                    96.86356343768793,
                    24.848542777253734,
                    0,
                    0,
                    -15.986465724980844,
                    62.317780594908875,
                    76.5566922962899,
                    0,
                    19.02322243409411,
                    -74.15554020821229,
                    64.3356267137516,
                    0,
                    1215107.7612304366,
                    -4736682.902037748,
                    4081926.095098698,
                    1
                },
                BoundingVolume = new BoundingVolume(),
                Content = null,
                Children = new List<TileElement>()
            }
        };
        
        var masterDescriptors = Directory.GetFiles(Path.Combine(destPath, "LOD-0"), "*.json");

        foreach (var descriptor in masterDescriptors)
        {
            var currentTileElement = tileset.Root;

            for (var lod = lods - 1; lod >= 0; lod--)
            {
                var box3 = JsonConvert.DeserializeObject<BoxDTO>(descriptor);

                var tile = new TileElement
                {
                    GeometricError = baseError / (1 << lod),
                    Refine = "REPLACE",
                    
                };
                
                currentTileElement.Children.Add(tile);
                currentTileElement = tile;
            }
            

        }

        /*
        for (var lod = 0; lod < lods; lod++)
        {
            var descriptors = Directory.GetFiles(Path.Combine(destPath, "LOD-" + lod), "*.json");

            foreach (var descriptor in descriptors)
            {
                var box3 = JsonConvert.DeserializeObject<BoxDTO>(descriptor);

                var tile = new TileElement
                {
                    GeometricError = baseError / (1 << lod),
                    Refine = "REPLACE",
                    BoundingVolume = new BoundingVolume
                    {
                        Box = new List<double> {}
                    }
                };

            }
        }*/

        File.WriteAllText("tileset.json", JsonConvert.SerializeObject(tileset, Formatting.None));

        // Generate tileset.json
    }

    private static void ConvertAllB3dm(string sourcePath, string destPath, int lods)
    {
        var filesToConvert = new List<Tuple<string, string>>();

        for (var lod = 0; lod < lods; lod++)
        {
            var files = Directory.GetFiles(Path.Combine(sourcePath, "LOD-" + lod), "*.obj");

            foreach (var file in files)
            {
                var outputFolder = Path.Combine(destPath, "LOD-" + lod);
                Directory.CreateDirectory(outputFolder);

                var outputFile = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(file), ".b3dm"));
                filesToConvert.Add(new Tuple<string, string>(file, outputFile));
                File.Copy(Path.ChangeExtension(file, ".json"), Path.ChangeExtension(outputFile, ".json"));
            }
        }

        Parallel.ForEach(filesToConvert, (file) => { ConvertB3dm(file.Item1, file.Item2); });
    }

    private static readonly GpsCoords DefaultGpsCoords = new()
    {
        Altitude = 0, // 120
        Latitude = 45.479,
        Longitude = 9.155
    };


    private static void ConvertB3dm(string objPath, string destPath)
    {
        var dir = Path.GetDirectoryName(objPath);
        var name = Path.GetFileNameWithoutExtension(objPath);

        var converter = Converter.MakeDefault();
        var outputFile = dir != null ? Path.Combine(dir, $"{name}.gltf") : $"{name}.gltf";

        converter.Convert(objPath, outputFile);

        var glbConv = new Gltf2GlbConverter();
        glbConv.Convert(new Gltf2GlbOptions(outputFile));

        File.Delete(outputFile);

        var glbFile = Path.ChangeExtension(outputFile, ".glb");

        var b3dm = new B3dm(File.ReadAllBytes(glbFile));

        File.WriteAllBytes(destPath, b3dm.ToBytes());
    }
}

public class GpsCoords
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
}