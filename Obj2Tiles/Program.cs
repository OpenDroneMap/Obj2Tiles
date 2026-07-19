using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using CommandLine;
using CommandLine.Text;
using Obj2Tiles.Library;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Stages;
using Obj2Tiles.Stages.Model;
using Obj2Tiles.Tiles;

namespace Obj2Tiles
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Accept enum option values case-insensitively (e.g. --texture-format webp) for a
            // friendlier CLI; option names keep their default (case-insensitive) handling.
            using var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = Console.Error;
            });

            var oResult = await parser.ParseArguments<Options>(args).WithParsedAsync(Run);

            if (oResult.Tag == ParserResultType.NotParsed)
            {
                Console.WriteLine("Usage: obj2tiles [options]");
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

                var decimateRes = await StagesFacade.Decimate(opts.Input, destFolderDecimation, opts.LODs);

                Console.WriteLine(" ?> Decimation stage done in {0}", sw.Elapsed);

                if (opts.StopAt == Stage.Decimation)
                    return;

                Console.WriteLine();
                Console.WriteLine(
                    $" => Splitting stage with {opts.Divisions} divisions {(opts.ZSplit ? "and Z-split" : "")}");

                destFolderSplit = opts.StopAt == Stage.Splitting
                    ? opts.Output
                    : createTempFolder($"{pipelineId}-obj2tiles-split");

                Console.WriteLine($" ?> Keep original textures: {opts.KeepOriginalTextures}, Split strategy: {opts.SplitPointStrategy}");

                var boundsMapper = await StagesFacade.Split(decimateRes.DestFiles, destFolderSplit, opts.Divisions,
                    opts.ZSplit, opts.KeepOriginalTextures, opts.SplitPointStrategy, opts.Octree, (float)opts.LodTextureScale,
                    opts.MaxTextureSize, opts.TextureQuality, opts.TextureFormat);

                Console.WriteLine(" ?> Splitting stage done in {0}", sw.Elapsed);

                if (opts.StopAt == Stage.Splitting)
                    return;

                var gpsCoords = opts.Latitude != null && opts.Longitude != null
                    ? new GpsCoords(opts.Latitude.Value, opts.Longitude.Value, opts.Altitude, opts.Scale, opts.YUpToZUp)
                    : null;

                Console.WriteLine();
                Console.WriteLine($" => Tiling stage {(gpsCoords != null ? $"with GPS coords {gpsCoords}" : "")}");

                // Geometric error must be expressed in the model's own coordinate units because it
                // drives the screen-space-error refinement of every 3D Tiles renderer. A fixed value
                // is meaningless for models that are not that size, so when the caller does not force
                // one (--error 0, the default) derive it from the model's bounding box diagonal.
                var b = decimateRes.Bounds;
                var modelDiagonal = Math.Sqrt(b.Width * b.Width + b.Height * b.Height + b.Depth * b.Depth);
                var baseError = opts.BaseError > 0 ? opts.BaseError : modelDiagonal;
                if (!(baseError > 0) || double.IsInfinity(baseError)) baseError = 1.0;

                // Coarsest decimated whole-model mesh, used to give the tileset root renderable content
                // (an empty root tile leaves the model invisible in renderers that do not descend into
                // the children of a content-less root). Run it through the split stage with 0 divisions,
                // which keeps it as a single mesh but compresses its textures, so the bootstrap root tile
                // stays small instead of embedding the full-resolution source textures.
                string? rootSourceObj = null;
                if (decimateRes.DestFiles.Length > 0)
                {
                    rootSourceObj = decimateRes.DestFiles[^1];
                    try
                    {
                        var rootTempDir = createTempFolder($"{pipelineId}-obj2tiles-root");
                        // The root is a bootstrap tile shown from far away, so downscale its textures at
                        // least as aggressively as the coarsest LOD and honour the absolute size cap.
                        var rootDownscale = (float)Math.Pow(opts.LodTextureScale, Math.Max(0, opts.LODs - 1));
                        await StagesFacade.Split(rootSourceObj, rootTempDir, 0,
                            textureDownscale: rootDownscale, maxTextureSize: opts.MaxTextureSize, textureQuality: opts.TextureQuality,
                            textureFormat: opts.TextureFormat);
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
                Console.WriteLine($" => Tiling stage with baseError {baseError:0.000}");

                sw.Restart();

                if (opts.LocalMode && (opts.Latitude != null || opts.Longitude != null))
                    Console.WriteLine(" !> Warning: --local overrides --lat/--lon. ECEF transform will not be applied.");

                StagesFacade.Tile(destFolderSplit, tilesetOutput, opts.LODs, baseError, boundsMapper, gpsCoords, opts.LocalMode, opts.Octree, rootSourceObj);

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

        private static bool CheckOptions(Options opts)
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

            if (opts.LodTextureScale is <= 0 or > 1)
            {
                Console.WriteLine(" !> --lod-texture-scale must be in the (0, 1] range");
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