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
    public static void Tile(string sourcePath, string destPath, int lods, double baseError, Dictionary<string, Box3>[] boundsMapper,
        GpsCoords? coords = null, bool localMode = false, bool isOctree = false, string? rootSourceObj = null, GltfConverterOptions? gltfOptions = null)
    {

        Console.WriteLine(" ?> Working on objs conversion");

        ConvertAllB3dm(sourcePath, destPath, lods, gltfOptions);

        // Give the tileset root renderable content. The root tile spans the whole model but, in an
        // octree/multi-tile layout, its geometry lives only in the child tiles, leaving the root
        // empty (content: null). An empty root is legal per the 3D Tiles spec, but several renderers
        // (e.g. giro3d's 3d-tiles-renderer, which hardcodes LOAD_ROOT_SIBLINGS) will not descend into
        // the children of a content-less root, so the whole model never appears. Converting the
        // coarsest decimated whole-model mesh into "root.b3dm" gives the root a lightweight, complete
        // representation that is then refined (REPLACE) by the finer child tiles.
        string? rootContentUri = null;
        if (rootSourceObj != null && File.Exists(rootSourceObj))
        {
            try
            {
                var rootB3dm = Path.Combine(destPath, "root.b3dm");
                Utils.ConvertB3dm(rootSourceObj, rootB3dm, gltfOptions);
                rootContentUri = "root.b3dm";
                Console.WriteLine($" ?> Generated root content from '{Path.GetFileName(rootSourceObj)}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($" !> Could not generate root content ({ex.Message}); the root tile will be empty.");
            }
        }

        Console.WriteLine(" -> Generating tileset.json");

        double[] rootTransform;

        if (localMode)
        {
            Console.WriteLine(" ?> Local mode: using identity matrix (no ECEF transform)");
            rootTransform = [
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            ];
        }
        else
        {
            if (coords == null)
            {
                Console.WriteLine(" ?> No --lat/--lon provided, using default coordinates (Milan). Use --local to disable ECEF transform.");
                coords = DefaultGpsCoords;
            }

            rootTransform = coords.ToEcefTransform();
        }

        // Generate tileset.json
        var tileset = new Tileset
        {
            Asset = new Asset { Version = "1.0" },
            GeometricError = baseError,
            Root = new TileElement
            {
                GeometricError = baseError,
                // Use REPLACE only when the root has actual content (coarse whole-model mesh):
                // the root is superseded by the finer child tiles so the two never render on top
                // of each other. Fall back to ADD when root content generation failed or was not
                // requested, so children are still rendered additively from a content-less root.
                Refine = rootContentUri != null ? "REPLACE" : "ADD",
                Transform = rootTransform,
                Content = rootContentUri != null ? new Content { Uri = rootContentUri } : null,
            }
        };

        var maxX = double.MinValue;
        var minX = double.MaxValue;
        var maxY = double.MinValue;
        var minY = double.MaxValue;
        var maxZ = double.MinValue;
        var minZ = double.MaxValue;

        if (isOctree)
        {
            // Build a parent lookup: for each tile in a finer LOD, find its parent in the next coarser LOD.
            // Tile names are hierarchical (e.g. "Mesh-XL-YL-XR-YR"), so a tile is a child of any coarser tile
            // whose name is a strict prefix (key + "-") of the tile's name.
            var lodParentMap = new Dictionary<string, string>();
            for (var lod = 0; lod < lods - 1; lod++)
            {
                foreach (var fineKey in boundsMapper[lod].Keys)
                {
                    // Each LOD differs from the next by exactly one split level, so the parent is the single coarser tile whose name is a strict prefix
                    var parent = boundsMapper[lod + 1].Keys
                        .FirstOrDefault(coarseKey => fineKey.StartsWith(coarseKey + "-"));
                    if (parent != null)
                        lodParentMap[fineKey] = parent;
                }
            }

            // Tile element cache so children can be appended when we reach finer LODs
            var tileMap = new Dictionary<string, TileElement>();

            // Process coarsest → finest so parents exist in tileMap before their children are added
            for (var lod = lods - 1; lod >= 0; lod--)
            {
                foreach (var (descriptor, box3) in boundsMapper[lod])
                {
                    if (box3.Min.X < minX) minX = box3.Min.X;
                    if (box3.Max.X > maxX) maxX = box3.Max.X;
                    if (box3.Min.Y < minY) minY = box3.Min.Y;
                    if (box3.Max.Y > maxY) maxY = box3.Max.Y;
                    if (box3.Min.Z < minZ) minZ = box3.Min.Z;
                    if (box3.Max.Z > maxZ) maxZ = box3.Max.Z;

                    var tile = new TileElement
                    {
                        // Coarser tiles have larger geometric error (seen from farther away);
                        // finest tiles are leaves with error = 0.
                        GeometricError = lod == 0 ? 0 : baseError / Math.Pow(2, lods - lod),
                        Refine = "REPLACE",
                        Content = new Content
                        {
                            Uri = $"LOD-{lod}/{Path.GetFileNameWithoutExtension(descriptor)}.b3dm"
                        },
                        BoundingVolume = box3.ToBoundingVolume()
                    };

                    tileMap[descriptor] = tile;

                    if (lodParentMap.TryGetValue(descriptor, out var parentKey))
                    {
                        tileMap[parentKey].Children ??= [];
                        tileMap[parentKey].Children!.Add(tile);
                    }
                    else
                    {
                        // No parent in a coarser LOD — attach directly to the root
                        tileset.Root!.Children ??= [];
                        tileset.Root!.Children!.Add(tile);
                    }
                }
            }
        }
        else
        {
            // Standard mode: all LODs produce the same set of tiles; each descriptor maps to a chain
            // LOD-(n-1) → LOD-(n-2) → ... → LOD-0 hanging from the root via REPLACE refinement.
            var masterDescriptors = boundsMapper[0].Keys;

            foreach (var descriptor in masterDescriptors)
            {
                var currentTileElement = tileset.Root;

                var refBox = boundsMapper[0][descriptor];

                for (var lod = lods - 1; lod >= 0; lod--)
                {
                    if (!boundsMapper[lod].TryGetValue(descriptor, out var box3)) continue;

                    if (box3.Min.X < minX) minX = box3.Min.X;
                    if (box3.Max.X > maxX) maxX = box3.Max.X;
                    if (box3.Min.Y < minY) minY = box3.Min.Y;
                    if (box3.Max.Y > maxY) maxY = box3.Max.Y;
                    if (box3.Min.Z < minZ) minZ = box3.Min.Z;
                    if (box3.Max.Z > maxZ) maxZ = box3.Max.Z;

                    var tile = new TileElement
                    {
                        GeometricError = lod == 0 ? 0 : CalculateGeometricError(refBox, box3, lod),
                        Refine = "REPLACE",
                        Content = new Content
                        {
                            Uri = $"LOD-{lod}/{Path.GetFileNameWithoutExtension(descriptor)}.b3dm"
                        },
                        BoundingVolume = box3.ToBoundingVolume()
                    };

                    currentTileElement.Children ??= [];
                    currentTileElement.Children.Add(tile);
                    currentTileElement = tile;
                }
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

    private static void ConvertAllB3dm(string sourcePath, string destPath, int lods, GltfConverterOptions? gltfOptions = null)
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
            Utils.ConvertB3dm(file.Item1, file.Item2, gltfOptions);
        });
    }

    // Duomo of Milan
    private static readonly GpsCoords DefaultGpsCoords = new()
    {
        Altitude = 0,
        Latitude = 45.46424200394995,
        Longitude = 9.190277486808588
    };

}