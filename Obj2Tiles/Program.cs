using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using CommandLine;
using CommandLine.Text;
using Obj2Tiles.Library;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Stages;
using Obj2Tiles.Stages.Model;
using Obj2Tiles.Tiles;
using SilentWave.Obj2Gltf;

namespace Obj2Tiles
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            // Accept enum option values case-insensitively (e.g. --texture-format webp) for a
            // friendlier CLI; option names keep their default (case-insensitive) handling.
            using var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = Console.Error;
            });

            var oResult = await parser.ParseArguments<Options>(args).WithParsedAsync(opts =>
            {
                ApplyPreset(opts, args);
                return Run(opts);
            });

            if (oResult.Tag == ParserResultType.NotParsed)
            {
                Console.WriteLine("Usage: obj2tiles [options]");
                return 1;
            }

            // Run() signals invalid options through Environment.ExitCode; surface it as the
            // process exit code so batch/CI callers (and the quality gate) can trust it.
            return Environment.ExitCode;
        }

        internal static void ApplyPreset(Options opts, string[] args)
        {
            if (opts.Preset == Preset.None) return;

            bool WasSpecified(params string[] flags) =>
                args.Any(a => flags.Contains(a) || Array.Exists(flags, f => a.StartsWith(f + "=")));

            switch (opts.Preset)
            {
                case Preset.Legacy:
                    if (!WasSpecified("-z", "--zsplit", "--no-zsplit"))
                        opts.NoZSplit = true;
                    if (!WasSpecified("--octree", "--no-octree"))
                        opts.NoOctree = true;
                    if (!WasSpecified("--lod-texture-scale"))
                        opts.LodTextureScale = 1.0;
                    break;

                case Preset.Standard:
                    if (!WasSpecified("-z", "--zsplit"))
                        opts.ZSplit = true;
                    if (!WasSpecified("--octree"))
                        opts.Octree = true;
                    // Explicit coordinates mean the caller wants a georeferenced tileset, which --local would discard.
                    if (!WasSpecified("--local", "--lat", "--lon"))
                        opts.LocalMode = true;
                    if (!WasSpecified("--lod-texture-scale"))
                        opts.LodTextureScale = 0.5;
                    if (!WasSpecified("-m", "--decimation-mode"))
                        opts.DecimationMode = DecimationMode.Quality;
                    if (!WasSpecified("--glb"))
                        opts.UseGlb = true;
                    if (!WasSpecified("--texture-quality"))
                        opts.TextureQuality = 80;
                    if (!WasSpecified("--fine-texture-quality"))
                        opts.FineTextureQuality = 90;
                    if (!WasSpecified("--max-texture-size"))
                        opts.MaxTextureSize = 8192;
                    break;
            }
        }

        private static async Task Run(Options opts)
        {
            Console.WriteLine();
            Console.WriteLine(" *** OBJ to Tiles ***");
            Console.WriteLine();

            // Invalid options are a failure: signal it to the caller so batch/CI runs do not
            // mistake a rejected configuration for a successful conversion.
            if (!CheckOptions(opts))
            {
                Environment.ExitCode = 1;
                return;
            }

            // --scale accepts decimals and fractions (see Options.TryParseScale); an invalid
            // value must fail before any output is written.
            if (!Options.TryParseScale(opts.Scale, out var scale, out var scaleError))
            {
                Console.WriteLine($" !> {scaleError}");
                Environment.ExitCode = 1;
                return;
            }

            opts.Output = Path.GetFullPath(opts.Output);
            opts.Input = Path.GetFullPath(opts.Input);

            // The output can be a loose folder tree or a single .3tz 3D Tiles Archive. The archive form is
            // selected by a .3tz extension on the output path or by the explicit --3tz flag; in that case the
            // tileset is written to a temporary folder and packed into the archive once tiling completes.
            var produce3tz = opts.Output.EndsWith(".3tz", StringComparison.OrdinalIgnoreCase) || opts.ThreeTz;
            string? archivePath = null;
            string tempBase;

            if (produce3tz)
            {
                archivePath = opts.Output.EndsWith(".3tz", StringComparison.OrdinalIgnoreCase)
                    ? opts.Output
                    : opts.Output.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".3tz";

                var archiveParent = Path.GetDirectoryName(archivePath);
                if (!string.IsNullOrEmpty(archiveParent))
                    Directory.CreateDirectory(archiveParent);
                tempBase = string.IsNullOrEmpty(archiveParent) ? Directory.GetCurrentDirectory() : archiveParent;
            }
            else
            {
                Directory.CreateDirectory(opts.Output);
                tempBase = opts.Output;
            }

            var pipelineId = Guid.NewGuid().ToString();
            var sw = new Stopwatch();
            var swg = Stopwatch.StartNew();

            // Actual base directory for intermediate temp folders, used for cleanup and user messages.
            var actualTempBase = opts.UseSystemTempFolder
                ? Path.GetTempPath()
                : Path.Combine(tempBase, ".temp");

            Func<string, string> createTempFolder = s => CreateTempFolder(s, actualTempBase);

            // Where the tiling stage writes the tileset: the output folder directly, or a temp folder that is
            // then packed into the .3tz archive.
            var tilesetOutput = produce3tz ? createTempFolder($"{pipelineId}-obj2tiles-tileset") : opts.Output;

            string? destFolderDecimation = null;
            string? destFolderSplit = null;

            try
            {

                destFolderDecimation = opts.StopAt == Stage.Decimation
                    ? opts.Output
                    : createTempFolder($"{pipelineId}-obj2tiles-decimation");

                Console.WriteLine($" => Decimation stage with {opts.LODs} LODs");
                sw.Start();

                var decimateRes = await StagesFacade.Decimate(opts.Input, destFolderDecimation, opts.LODs, opts.DecimationMode,
                    opts.IgnoreNormalMaps);

                Console.WriteLine(" ?> Decimation stage done in {0}", sw.Elapsed);

                if (opts.StopAt == Stage.Decimation)
                    return;

                Console.WriteLine();
                Console.WriteLine(
                    $" => Splitting stage with {opts.Divisions} divisions {(opts.EffectiveZSplit ? "and Z-split" : "")}");

                destFolderSplit = opts.StopAt == Stage.Splitting
                    ? opts.Output
                    : createTempFolder($"{pipelineId}-obj2tiles-split");

                Console.WriteLine(
                    $" ?> Keep original textures: {opts.KeepOriginalTextures}, Single material per part: {opts.SingleMaterialPerPart}, Split strategy: {opts.SplitPointStrategy}");

                var boundsMapper = await StagesFacade.Split(decimateRes.DestFiles, destFolderSplit, opts.Divisions,
                    opts.EffectiveZSplit, opts.KeepOriginalTextures, opts.SplitPointStrategy, opts.EffectiveOctree, (float)opts.LodTextureScale,
                    opts.Overlap, opts.IgnoreNormalMaps, opts.MaxTextureSize, opts.TextureQuality, opts.TextureFormat, opts.FineTextureQuality,
                    opts.SingleMaterialPerPart);

                Console.WriteLine(" ?> Splitting stage done in {0}", sw.Elapsed);

                if (opts.StopAt == Stage.Splitting)
                    return;

                var gpsCoords = opts.Latitude != null && opts.Longitude != null
                    ? new GpsCoords(opts.Latitude.Value, opts.Longitude.Value, opts.Altitude, scale, opts.YUpToZUp)
                    : null;

                Console.WriteLine();
                Console.WriteLine($" => Tiling stage {(gpsCoords != null ? $"with GPS coords {gpsCoords}" : "")}");

                // Geometric error must be expressed in the model's own coordinate units because it
                // drives the screen-space-error refinement of every 3D Tiles renderer. When the caller
                // does not force one (--error omitted), it is derived automatically inside the Tiling
                // stage from the coarsest LOD using --error-estimation-mode/--error-factor.
                var baseError = opts.BaseError;

                // Coarsest decimated whole-model mesh, used to give the tileset root renderable content
                // (an empty root tile leaves the model invisible in renderers that do not descend into
                // the children of a content-less root). Run it through the split stage with 0 divisions,
                // which keeps it as a single mesh but compresses its textures, so the bootstrap root tile
                // stays small instead of embedding the full-resolution source textures.
                string? rootSourceObj = null;
                if (!opts.NoRootContent && decimateRes.DestFiles.Length > 0)
                {
                    rootSourceObj = decimateRes.DestFiles[^1];
                    try
                    {
                        var rootTempDir = createTempFolder($"{pipelineId}-obj2tiles-root");
                        // The root is a bootstrap tile shown from far away, so downscale its textures at
                        // least as aggressively as the coarsest LOD and honour the absolute size cap.
                        var rootDownscale = (float)Math.Pow(opts.LodTextureScale, Math.Max(0, opts.LODs - 1));
                        // The root spans the whole model as a single (un-split) mesh, so without an
                        // absolute cap a texture-heavy source (e.g. an ODM model with dozens of
                        // 8192x8192 textures) yields a root tile that decodes to hundreds of MB of GPU
                        // memory. That single tile can exceed a web viewer's tile-cache budget and stall
                        // progressive loading, leaving the model invisible. Since the root is only shown
                        // from far away (or briefly, while finer LODs stream in) its texture detail is
                        // irrelevant, so cap it hard here regardless of --max-texture-size (honouring a
                        // smaller user cap when one is set). 256px keeps the root - the first tile the
                        // viewer downloads - small (a few MB) without any visible loss at overview zoom.
                        const int rootTextureSizeCap = 256;

                        // When SingleMaterialPerPart is used, we do however know that there can only be
                        // a single texture for the root tiles. In this case we don't need to enforce a
                        // per-texture cap, but rather a max-size for the one texture we have.
                        const int singleMaterialTextureSizeCap = 2048;

                        int rootMaxTextureSize;

                        if (opts.SingleMaterialPerPart)
                        {
                            // MaxTextureSize == 0 means "no cap" and must not disable the root bound.
                            rootMaxTextureSize = opts.MaxTextureSize > 0
                                ? Math.Min(opts.MaxTextureSize, singleMaterialTextureSizeCap)
                                : singleMaterialTextureSizeCap;
                        }
                        else
                        {
                            rootMaxTextureSize = opts.MaxTextureSize > 0
                                ? Math.Min(opts.MaxTextureSize, rootTextureSizeCap)
                                : rootTextureSizeCap;
                        }

                        await StagesFacade.Split(rootSourceObj, rootTempDir, 0,
                            textureDownscale: rootDownscale, maxTextureSize: rootMaxTextureSize, textureQuality: opts.TextureQuality,
                            textureFormat: opts.TextureFormat, ignoreNormalMaps: opts.IgnoreNormalMaps,
                            singleMaterialPerPart: opts.SingleMaterialPerPart);
                        var compressedRoot = Directory.GetFiles(rootTempDir, "*.obj").FirstOrDefault();
                        if (compressedRoot != null)
                            rootSourceObj = compressedRoot;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" !> Could not compress root mesh ({ex.Message}); using full-resolution coarsest mesh.");
                    }
                }

                Console.WriteLine();
                Console.WriteLine(baseError.HasValue
                    ? $" => Tiling stage with baseError {baseError}"
                    : $" => Tiling stage with auto-computed baseError ({opts.ErrorEstimationMode})");

                sw.Restart();

                if (opts.LocalMode && (opts.Latitude != null || opts.Longitude != null))
                    Console.WriteLine(" !> Warning: --local overrides --lat/--lon. ECEF transform will not be applied.");

                // Build glTF conversion options. Unlit marks materials with KHR_materials_unlit. KTX2
                // (Basis Universal) textures cut GPU/VRAM usage several-fold versus decoded JPEG/WebP,
                // which is the dominant cost for texture-heavy tilesets; they are produced here
                // in-process via the bundled libktx native library (P/Invoke).
                var gltfOptions = new GltfConverterOptions { UnlitMaterials = opts.Unlit };
                if (opts.TextureFormat == TextureFormat.Ktx2)
                {
                    gltfOptions.EncodeKtx2 = true;
                    gltfOptions.Ktx2Uastc = opts.Ktx2Uastc;
                    gltfOptions.Ktx2QualityLevel = opts.Ktx2Quality;
                    gltfOptions.Ktx2Threads = opts.Ktx2Threads;
                    gltfOptions.Ktx2ZstdLevel = opts.Ktx2ZstdLevel;
                    gltfOptions.KtxToolPath = opts.KtxPath;
                }

                StagesFacade.Tile(destFolderSplit, tilesetOutput, opts.LODs, baseError, boundsMapper, gpsCoords, opts.LocalMode, opts.EffectiveOctree,
                    opts.ErrorEstimationMode, opts.ErrorFactor, opts.EffectiveUseGlb, opts.LodTextureScale, rootSourceObj, gltfOptions);

                Console.WriteLine(" ?> Tiling stage done in {0}", sw.Elapsed);

                if (produce3tz)
                {
                    Console.WriteLine();
                    Console.WriteLine($" => Packing 3D Tiles Archive '{archivePath}'");
                    var compressionLevel = ThreeTzArchive.ResolveCompressionLevel(opts.ThreeTzCompression);
                    var entryCount = ThreeTzArchive.CreateFromDirectory(tilesetOutput, archivePath!, compressionLevel);
                    Console.WriteLine($" ?> 3TZ archive created with {entryCount} entries");
                }
            }
            catch (Exception ex)
            {
                // Signal failure to the caller: without a non-zero exit code a failed conversion
                // (e.g. a missing .mtl dependency) would look successful to batch/CI callers even
                // though no output was produced.
                Console.Error.WriteLine(" !> Exception: {0}", ex.Message);
                Environment.ExitCode = 1;
            }
            finally
            {
                Console.WriteLine();
                var outcome = Environment.ExitCode == 0 ? "completed" : "failed";
                Console.WriteLine(" => Pipeline {0} in {1}", outcome, swg.Elapsed);

                var tmpFolder = actualTempBase;

                if (opts.KeepIntermediateFiles)
                {
                    Console.WriteLine(
                        $" ?> Skipping cleanup, intermediate files are in '{tmpFolder}' with pipeline id '{pipelineId}'");

                    Console.WriteLine(" ?> You should delete this folder manually, it is only for debugging purposes");
                }
                else
                {

                    Console.WriteLine(" => Cleaning up");

                    if (destFolderDecimation != null && destFolderDecimation != opts.Output)
                        Directory.Delete(destFolderDecimation, true);

                    if (destFolderSplit != null && destFolderSplit != opts.Output)
                        Directory.Delete(destFolderSplit, true);

                    if (produce3tz && Directory.Exists(tilesetOutput))
                        Directory.Delete(tilesetOutput, true);

                    if (!opts.UseSystemTempFolder && Directory.Exists(tmpFolder))
                        Directory.Delete(tmpFolder, true);

                    Console.WriteLine(" ?> Cleaning up ok");
                }
            }
        }

        internal static bool CheckOptions(Options opts)
        {

            if (string.IsNullOrWhiteSpace(opts.Input))
            {
                Console.WriteLine(" !> Input file is required");
                return false;
            }

            if (!File.Exists(opts.Input))
            {
                Console.WriteLine(" !> Input file does not exist");
                return false;
            }

            if (string.IsNullOrWhiteSpace(opts.Output))
            {
                Console.WriteLine(" !> Output folder is required");
                return false;
            }

            if (opts.LODs < 1)
            {
                Console.WriteLine(" !> LODs must be at least 1");
                return false;
            }

            if (opts.Divisions < 0)
            {
                Console.WriteLine(" !> Divisions must be non-negative");
                return false;
            }

            var wants3tz = opts.Output.EndsWith(".3tz", StringComparison.OrdinalIgnoreCase) || opts.ThreeTz;
            if (wants3tz && opts.StopAt != Stage.Tiling)
            {
                Console.WriteLine(" !> 3TZ output requires the full Tiling stage (do not set --stage to Decimation or Splitting)");
                return false;
            }

            if (opts.ThreeTzCompression is < 0 or > 9)
            {
                Console.WriteLine(" !> --3tz-compression must be between 0 and 9");
                return false;
            }

            if (opts.MaxTextureSize < 0)
            {
                Console.WriteLine(" !> --max-texture-size must be non-negative (0 disables the cap)");
                return false;
            }

            if (opts.TextureQuality is < 1 or > 100)
            {
                Console.WriteLine(" !> --texture-quality must be between 1 and 100");
                return false;
            }

            if (opts.FineTextureQuality is < 0 or > 100)
            {
                Console.WriteLine(" !> --fine-texture-quality must be between 0 and 100 (0 falls back to --texture-quality)");
                return false;
            }

            if (opts.LodTextureScale is <= 0 or > 1)
            {
                Console.WriteLine(" !> --lod-texture-scale must be in the (0, 1] range");
                return false;
            }

            if (opts.BaseError is { } baseError)
            {
                if (baseError == 0)
                {
                    // Older releases documented --error 0 as "derive automatically"; keep that meaning.
                    Console.WriteLine(" ?> --error 0 means auto: the geometric error will be estimated from the mesh");
                    opts.BaseError = null;
                }
                else if (!(baseError > 0) || !double.IsFinite(baseError))
                {
                    Console.WriteLine(" !> --error must be a positive finite number (or 0 / omitted for auto)");
                    return false;
                }
            }

            if (opts.ErrorFactor is { } errorFactor && (!(errorFactor > 0) || !double.IsFinite(errorFactor)))
            {
                Console.WriteLine(" !> --error-factor must be a positive finite number");
                return false;
            }

            if (!(opts.Overlap >= 0) || !double.IsFinite(opts.Overlap))
            {
                Console.WriteLine(" !> --overlap must be a non-negative finite number");
                return false;
            }

            if (opts.Ktx2Threads < 0)
            {
                Console.WriteLine(" !> --ktx2-threads must be non-negative (0 preserves the current default)");
                return false;
            }

            if (opts.Ktx2Threads > 0 && opts.TextureFormat != TextureFormat.Ktx2)
            {
                Console.WriteLine(" !> --ktx2-threads requires --texture-format Ktx2");
                return false;
            }

            if (opts.Ktx2ZstdLevel is < 0 or > 22)
            {
                Console.WriteLine(" !> --ktx2-zstd-level must be 0 (disabled) or between 1 and 22");
                return false;
            }

            if (opts.Ktx2ZstdLevel > 0 && opts.TextureFormat != TextureFormat.Ktx2)
            {
                Console.WriteLine(" !> --ktx2-zstd-level requires --texture-format Ktx2");
                return false;
            }

            if (opts.Ktx2ZstdLevel > 0 && !opts.Ktx2Uastc)
            {
                Console.WriteLine(" !> --ktx2-zstd-level requires --ktx2-uastc");
                return false;
            }

            return true;
        }


        private static string CreateTempFolder(string folderName, string baseFolder)
        {
            var tempFolder = Path.Combine(baseFolder, folderName);
            Directory.CreateDirectory(tempFolder);
            return tempFolder;
        }
    }
}
