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
    public static void Tile(string sourcePath, string destPath, int lods, Dictionary<string, Box3>[] boundsMapper,
        GpsCoords? coords = null)
    {

        Console.WriteLine(" ?> Working on objs conversion");
        
        ConvertAllB3dm(sourcePath, destPath, lods);

        Console.WriteLine(" -> Generating tileset.json");

        if (coords == null)
        {
            Console.WriteLine(" ?> Using default coordinates");
            coords = DefaultGpsCoords;
        }
       
        // Don't ask me why 100, I have no idea but it works
        // https://github.com/CesiumGS/3d-tiles/issues/162
        const int baseError = 100;

        // Generate tileset.json
        var tileset = new Tileset
        {
            Asset = new Asset { Version = "1.0" },
            GeometricError = baseError,
            Root = new TileElement
            {
                GeometricError = baseError,
                Refine = "ADD",

                Transform = coords.ToEcefTransform(),
                Children = new List<TileElement>()
            }
        };

        var maxX = double.MinValue;
        var minX = double.MaxValue;
        var maxY = double.MinValue;
        var minY = double.MaxValue;
        var maxZ = double.MinValue;
        var minZ = double.MaxValue;

        var masterDescriptors = boundsMapper[0].Keys;
        
        foreach (var descriptor in masterDescriptors)
        {
            var currentTileElement = tileset.Root;
            
            var refBox = boundsMapper[0][descriptor];

            for (var lod = lods - 1; lod >= 0; lod--)
            {
                var box3 = boundsMapper[lod][descriptor];

                if (box3.Min.X < minX)
                    minX = box3.Min.X;

                if (box3.Max.X > maxX)
                    maxX = box3.Max.X;

                if (box3.Min.Y < minY)
                    minY = box3.Min.Y;

                if (box3.Max.Y > maxY)
                    maxY = box3.Max.Y;

                if (box3.Min.Z < minZ)
                    minZ = box3.Min.Z;

                if (box3.Max.Z > maxZ)
                    maxZ = box3.Max.Z;

                var tile = new TileElement
                {
                    GeometricError = lod == 0 ? 0 : CalculateGeometricError(refBox, box3, lod),
                    Refine = "REPLACE",
                    Children = new List<TileElement>(),
                    Content = new Content
                    {
                        Uri = $"LOD-{lod}/{Path.GetFileNameWithoutExtension(descriptor)}.b3dm"
                    },
                    BoundingVolume = box3.ToBoundingVolume()
                };

                currentTileElement.Children.Add(tile);
                currentTileElement = tile;
            }
        }

        var globalBox = new Box3(minX, minY, minZ, maxX, maxY, maxZ);

        tileset.Root.BoundingVolume = globalBox.ToBoundingVolume();

        File.WriteAllText(Path.Combine(destPath, "tileset.json"),
            JsonConvert.SerializeObject(tileset, Formatting.Indented));
    }

    // Calculate mesh geometric error
    private static double CalculateGeometricError(Box3 refBox, Box3 box, int lod)
    {

        var dW = Math.Abs(refBox.Width - box.Width) / box.Width + 1;
        var dH = Math.Abs(refBox.Height - box.Height) / box.Height + 1;
        var dD = Math.Abs(refBox.Depth - box.Depth) / box.Depth + 1;
       
        return Math.Pow(dW + dH + dD, lod);

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
            }
        }

        Parallel.ForEach(filesToConvert, (file) =>
        {
            Console.WriteLine($" -> Converting to b3dm '{file.Item1}'");
            Utils.ConvertB3dm(file.Item1, file.Item2);
        });
    }

    // Where is it?
    private static readonly GpsCoords DefaultGpsCoords = new()
    {
        Altitude = 0,
        Latitude = 45.46424200394995,
        Longitude = 9.190277486808588
    };

}