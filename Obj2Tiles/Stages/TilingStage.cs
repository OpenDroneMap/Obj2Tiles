using System.Diagnostics;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Stages.Model;
using Obj2Tiles.Tiles;
using SilentWave;
using SilentWave.Obj2Gltf;

namespace Obj2Tiles.Stages;

public static partial class StagesFacade
{
    public static void Tile(string sourcePath, string destPath, int lods, double? baseError, Dictionary<string, TileBounds>[] boundsMapper,
        GpsCoords? coords = null, bool localMode = false, bool isOctree = false,
        ErrorEstimationMode errorEstimationMode = ErrorEstimationMode.AverageEdgeLength, double? errorFactor = null,
        bool useGlb = false, double lodTextureScale = 1.0, string? rootSourceObj = null, GltfConverterOptions? gltfOptions = null)
    {

        Console.WriteLine(" ?> Working on objs conversion");

        var tileExtension = useGlb ? ".glb" : ".b3dm";

        ConvertAllTiles(sourcePath, destPath, lods, useGlb, gltfOptions);

        // Give the tileset root renderable content. The root tile spans the whole model but, in an
        // octree/multi-tile layout, its geometry lives only in the child tiles, leaving the root
        // empty (content: null). An empty root is legal per the 3D Tiles spec, but several renderers
        // (e.g. giro3d's 3d-tiles-renderer, which hardcodes LOAD_ROOT_SIBLINGS) will not descend into
        // the children of a content-less root, so the whole model never appears. Converting the
        // coarsest decimated whole-model mesh into a root tile gives the root a lightweight, complete
        // representation that is then refined (REPLACE) by the finer child tiles.
        string? rootContentUri = null;
        TileBounds? rootBounds = null;

        if (rootSourceObj != null && File.Exists(rootSourceObj))
        {
            try
            {
                var rootFileName = "root" + tileExtension;
                var rootTile = Path.Combine(destPath, rootFileName);
                if (useGlb)
                    Utils.ConvertGlb(rootSourceObj, rootTile, gltfOptions);
                else
                    Utils.ConvertB3dm(rootSourceObj, rootTile, gltfOptions);
                rootContentUri = rootFileName;

                var rootMesh = MeshUtils.LoadMesh(rootSourceObj, out _, false);
                rootBounds = new TileBounds(rootMesh.Bounds, rootMesh.AverageEdgeLength,
                    rootMesh.MaximumEdgeLength, rootMesh.FacesCount);

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

        var errorFactorValue = ResolveErrorFactor(errorEstimationMode, errorFactor);
        var isToplevel = IsToplevelMode(errorEstimationMode);

        if (baseError is { } requestedError && (!(requestedError > 0) || !double.IsFinite(requestedError)))
            baseError = null;

        // If no --error was passed, derive the root's geometric error from the coarsest LOD using the
        // chosen mode's metric (bounding-box diagonal, or average/maximum triangle edge length), so it
        // scales with the actual mesh size and detail instead of relying on a fixed default like 100.
        //
        // The root's texture is downscaled by the same lodTextureScale^(lods-1) factor as the coarsest
        // LOD (see Program.cs rootDownscale), but is then additionally hard-capped at 256px - a cut the
        // coarsest LOD tile (capped only at --max-texture-size) doesn't take. That extra loss isn't
        // captured by lods-1, so using lods here (one multiplier step worse) is a conservative floor on
        // the root's real quality loss, not an exact measure of the 256px cap's effect.
        var rootGeometricError = baseError ?? EstimateRootMetric(boundsMapper, errorEstimationMode, rootBounds) * errorFactorValue
            * LodTextureQualityMultiplier(lods, lodTextureScale);

        if (baseError == null)
            Console.WriteLine($" ?> No --error provided, auto-computed root geometric error: {rootGeometricError:0.00} ({errorEstimationMode}, factor {errorFactorValue}, texture-quality multiplier {LodTextureQualityMultiplier(lods, lodTextureScale):0.00})");

        // Toplevel* modes cascade from the root by pure halving (the root already carries the texture
        // multiplier); per-tile modes scale their own estimate. Either way a tile never claims more error
        // than its parent, otherwise renderers refine straight through it and that LOD is never shown.
        double TileGeometricError(int lod, TileBounds tileBounds, double parentError)
        {
            if (lod == 0) return 0;

            var error = isToplevel
                ? rootGeometricError / Math.Pow(2, lods - lod)
                : EstimateGeometricError(tileBounds, errorEstimationMode, errorFactorValue)
                  * LodTextureQualityMultiplier(lod, lodTextureScale);

            return Math.Min(error, parentError);
        }

        // Generate tileset.json
        var tileset = new Tileset
        {
            // Plain glTF/GLB tile content requires 3D Tiles 1.1; b3dm-wrapped content stays on 1.0.
            Asset = new Asset { Version = useGlb ? "1.1" : "1.0" },
            GeometricError = rootGeometricError,
            Root = new TileElement
            {
                GeometricError = rootGeometricError,
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
                foreach (var (descriptor, tileBounds) in boundsMapper[lod])
                {
                    var box3 = tileBounds.Box;

                    if (box3.Min.X < minX) minX = box3.Min.X;
                    if (box3.Max.X > maxX) maxX = box3.Max.X;
                    if (box3.Min.Y < minY) minY = box3.Min.Y;
                    if (box3.Max.Y > maxY) maxY = box3.Max.Y;
                    if (box3.Min.Z < minZ) minZ = box3.Min.Z;
                    if (box3.Max.Z > maxZ) maxZ = box3.Max.Z;

                    var hasParent = lodParentMap.TryGetValue(descriptor, out var parentKey);
                    var parentTile = hasParent ? tileMap[parentKey!] : tileset.Root!;

                    var tile = new TileElement
                    {
                        GeometricError = TileGeometricError(lod, tileBounds, parentTile.GeometricError),
                        Refine = "REPLACE",
                        Content = new Content
                        {
                            Uri = $"LOD-{lod}/{Path.GetFileNameWithoutExtension(descriptor)}{tileExtension}"
                        },
                        BoundingVolume = box3.ToBoundingVolume()
                    };

                    tileMap[descriptor] = tile;

                    // Tiles without a parent in a coarser LOD attach directly to the root
                    parentTile.Children ??= [];
                    parentTile.Children!.Add(tile);
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

                for (var lod = lods - 1; lod >= 0; lod--)
                {
                    if (!boundsMapper[lod].TryGetValue(descriptor, out var tileBounds)) continue;

                    var box3 = tileBounds.Box;

                    if (box3.Min.X < minX) minX = box3.Min.X;
                    if (box3.Max.X > maxX) maxX = box3.Max.X;
                    if (box3.Min.Y < minY) minY = box3.Min.Y;
                    if (box3.Max.Y > maxY) maxY = box3.Max.Y;
                    if (box3.Min.Z < minZ) minZ = box3.Min.Z;
                    if (box3.Max.Z > maxZ) maxZ = box3.Max.Z;

                    var tile = new TileElement
                    {
                        GeometricError = TileGeometricError(lod, tileBounds, currentTileElement.GeometricError),
                        Refine = "REPLACE",
                        Content = new Content
                        {
                            Uri = $"LOD-{lod}/{Path.GetFileNameWithoutExtension(descriptor)}{tileExtension}"
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

    // Union of all per-tile bounding boxes across every LOD, giving the overall model extent
    private static Box3 ComputeGlobalBounds(Dictionary<string, TileBounds>[] boundsMapper)
    {
        var maxX = double.MinValue;
        var minX = double.MaxValue;
        var maxY = double.MinValue;
        var minY = double.MaxValue;
        var maxZ = double.MinValue;
        var minZ = double.MaxValue;

        foreach (var box in boundsMapper.SelectMany(lodMap => lodMap.Values).Select(tb => tb.Box))
        {
            if (box.Min.X < minX) minX = box.Min.X;
            if (box.Max.X > maxX) maxX = box.Max.X;
            if (box.Min.Y < minY) minY = box.Min.Y;
            if (box.Max.Y > maxY) maxY = box.Max.Y;
            if (box.Min.Z < minZ) minZ = box.Min.Z;
            if (box.Max.Z > maxZ) maxZ = box.Max.Z;
        }

        return new Box3(minX, minY, minZ, maxX, maxY, maxZ);
    }

    private static bool IsToplevelMode(ErrorEstimationMode mode) => mode is
        ErrorEstimationMode.ToplevelBoundingBoxDiagonal or
        ErrorEstimationMode.ToplevelAverageEdgeLength or
        ErrorEstimationMode.ToplevelMaximumEdgeLength;

    // Coarser LODs don't just have simpler geometry - their textures are also downscaled (see
    // --lod-texture-scale in SplitStage), so they look progressively blurrier even where the
    // surface itself barely changes between LODs. Mirrors SplitStage's textureDownscale formula
    // (lodTextureScale^lod for lod > 0, full resolution at lod 0) and inverts it, so geometric
    // error grows to reflect texture quality loss too, not just geometric simplification.
    private static double LodTextureQualityMultiplier(int lod, double lodTextureScale) =>
        lod == 0 ? 1.0 : 1.0 / Math.Pow(Math.Clamp(lodTextureScale, 1e-6, 1.0), lod);

    private static double ResolveErrorFactor(ErrorEstimationMode mode, double? errorFactor)
    {
        if (errorFactor.HasValue) return errorFactor.Value;

        return mode switch
        {
            ErrorEstimationMode.BoundingBoxDiagonal or ErrorEstimationMode.ToplevelBoundingBoxDiagonal => 0.1,
            // Higher than the metric's "1x edge length" naive baseline: viewers pick finer LODs
            // once a tile's projected screen-space error crosses their threshold, so a factor
            // tuned too low (previously 0.5) makes coarse tiles look "good enough" for too long,
            // delaying refinement to finer LODs well past where it visually should kick in.
            ErrorEstimationMode.AverageEdgeLength or ErrorEstimationMode.ToplevelAverageEdgeLength => 1.0,
            ErrorEstimationMode.MaximumEdgeLength or ErrorEstimationMode.ToplevelMaximumEdgeLength => 1.0,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    // Below this many faces, an edge-length average/maximum isn't statistically meaningful (e.g. a
    // single sliver triangle), so edge-length-based modes fall back to a bounding-box estimate instead.
    private const int MinFacesForEdgeLengthEstimate = 4;

    // Fixed factor used only for the degenerate-tile fallback above, independent of --error-factor
    // (which is calibrated for the chosen mode's own metric, not for a diagonal-based substitute).
    private const double DegenerateTileDiagonalFactor = 0.1;

    // Per-tile geometric error, estimated directly from that tile's own mesh: average/maximum triangle
    // edge length reflects the actual detail discarded by decimation, unlike bounding-box size which
    // only reflects spatial footprint. Not used for Toplevel* modes, which cascade from the root instead.
    private static double EstimateGeometricError(TileBounds tileBounds, ErrorEstimationMode mode, double factor)
    {
        if (mode is ErrorEstimationMode.AverageEdgeLength or ErrorEstimationMode.MaximumEdgeLength
            && tileBounds.FacesCount < MinFacesForEdgeLengthEstimate)
            return tileBounds.Box.Diagonal() * DegenerateTileDiagonalFactor;

        return mode switch
        {
            ErrorEstimationMode.BoundingBoxDiagonal => tileBounds.Box.Diagonal() * factor,
            ErrorEstimationMode.AverageEdgeLength => tileBounds.AverageEdgeLength * factor,
            ErrorEstimationMode.MaximumEdgeLength => tileBounds.MaximumEdgeLength * factor,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Toplevel modes don't use per-tile estimation")
        };
    }

    // Metric feeding the root/tileset geometric error. Prefer rootBounds — measured from the
    // actual root mesh — over the coarsest-LOD fallback below, which estimates from a different,
    // separately-decimated file (the LOD-1 tiles) and previously produced a root error barely
    // bigger than its own child's, so the root never satisfied refinement into it.
    private static double EstimateRootMetric(Dictionary<string, TileBounds>[] boundsMapper, ErrorEstimationMode mode,
        TileBounds? rootBounds)
    {
        if (rootBounds is { } rb)
        {
            if (mode is ErrorEstimationMode.AverageEdgeLength or ErrorEstimationMode.MaximumEdgeLength
                    or ErrorEstimationMode.ToplevelAverageEdgeLength or ErrorEstimationMode.ToplevelMaximumEdgeLength
                && rb.FacesCount < MinFacesForEdgeLengthEstimate)
                return rb.Box.Diagonal() * DegenerateTileDiagonalFactor;

            return mode switch
            {
                ErrorEstimationMode.BoundingBoxDiagonal or ErrorEstimationMode.ToplevelBoundingBoxDiagonal =>
                    rb.Box.Diagonal(),
                ErrorEstimationMode.AverageEdgeLength or ErrorEstimationMode.ToplevelAverageEdgeLength =>
                    rb.AverageEdgeLength,
                ErrorEstimationMode.MaximumEdgeLength or ErrorEstimationMode.ToplevelMaximumEdgeLength =>
                    rb.MaximumEdgeLength,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }

        var coarsestLod = boundsMapper[^1].Values;

        return mode switch
        {
            ErrorEstimationMode.BoundingBoxDiagonal or ErrorEstimationMode.ToplevelBoundingBoxDiagonal =>
                ComputeGlobalBounds(boundsMapper).Diagonal(),
            ErrorEstimationMode.AverageEdgeLength or ErrorEstimationMode.ToplevelAverageEdgeLength =>
                WeightedAverageEdgeLength(coarsestLod),
            ErrorEstimationMode.MaximumEdgeLength or ErrorEstimationMode.ToplevelMaximumEdgeLength =>
                coarsestLod.Select(tb => tb.MaximumEdgeLength).DefaultIfEmpty(0).Max(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    // Face-count-weighted average edge length across a set of tiles, so tiles with more geometry
    // contribute proportionally more to the aggregate than a plain per-tile average would.
    private static double WeightedAverageEdgeLength(IEnumerable<TileBounds> tiles)
    {
        var totalFaces = 0L;
        var weightedSum = 0.0;

        foreach (var tb in tiles)
        {
            totalFaces += tb.FacesCount;
            weightedSum += tb.AverageEdgeLength * tb.FacesCount;
        }

        return totalFaces == 0 ? 0 : weightedSum / totalFaces;
    }

    private static void ConvertAllTiles(string sourcePath, string destPath, int lods, bool useGlb, GltfConverterOptions? gltfOptions = null)
    {
        var tileExtension = useGlb ? ".glb" : ".b3dm";
        var filesToConvert = new List<Tuple<string, string>>();

        for (var lod = 0; lod < lods; lod++)
        {
            var files = Directory.GetFiles(Path.Combine(sourcePath, "LOD-" + lod), "*.obj");

            foreach (var file in files)
            {
                var outputFolder = Path.Combine(destPath, "LOD-" + lod);
                Directory.CreateDirectory(outputFolder);

                var outputFile = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(file), tileExtension));
                filesToConvert.Add(new Tuple<string, string>(file, outputFile));
            }
        }

        Parallel.ForEach(filesToConvert, (file) =>
        {
            Console.WriteLine($" -> Converting to {tileExtension.TrimStart('.')} '{file.Item1}'");

            if (useGlb)
                Utils.ConvertGlb(file.Item1, file.Item2, gltfOptions);
            else
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

/// <summary>
/// How per-tile (and root) geometric error is estimated.
/// </summary>
public enum ErrorEstimationMode
{
    /// <summary>Each tile's error is its own bounding-box diagonal times --error-factor (default 0.1).</summary>
    BoundingBoxDiagonal,

    /// <summary>Each tile's error is its own average triangle edge length times --error-factor (default 1.0).</summary>
    AverageEdgeLength,

    /// <summary>Each tile's error is its own maximum triangle edge length times --error-factor (default 1.0).</summary>
    MaximumEdgeLength,

    /// <summary>
    /// The root's error is the coarsest LOD's bounding-box diagonal times --error-factor (default 0.1);
    /// every tile's error is then that root value halved once per LOD subdivision from the root.
    /// </summary>
    ToplevelBoundingBoxDiagonal,

    /// <summary>
    /// The root's error is the coarsest LOD's (face-weighted) average edge length times --error-factor
    /// (default 1.0); every tile's error is then that root value halved once per LOD subdivision from the root.
    /// </summary>
    ToplevelAverageEdgeLength,

    /// <summary>
    /// The root's error is the coarsest LOD's maximum edge length times --error-factor (default 1.0);
    /// every tile's error is then that root value halved once per LOD subdivision from the root.
    /// </summary>
    ToplevelMaximumEdgeLength
}
