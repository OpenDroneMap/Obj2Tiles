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

namespace Obj2Tiles
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var oResult = await Parser.Default.ParseArguments<Options>(args).WithParsedAsync(Run);

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

            if (!CheckOptions(opts)) return;

            opts.Output = Path.GetFullPath(opts.Output);
            opts.Input = Path.GetFullPath(opts.Input);

            Directory.CreateDirectory(opts.Output);

            var pipelineId = Guid.NewGuid().ToString();
            var sw = new Stopwatch();
            var swg = Stopwatch.StartNew();

            Func<string, string> createTempFolder = opts.UseSystemTempFolder
                ? s => CreateTempFolder(s, Path.GetTempPath())
                : s => CreateTempFolder(s, Path.Combine(opts.Output, ".temp"));

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
                    opts.ZSplit, opts.KeepOriginalTextures, opts.SplitPointStrategy, opts.Octree, (float)opts.LodTextureScale);

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
                        await StagesFacade.Split(rootSourceObj, rootTempDir, 0);
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

                StagesFacade.Tile(destFolderSplit, opts.Output, opts.LODs, baseError, boundsMapper, gpsCoords, opts.LocalMode, opts.Octree, rootSourceObj);

                Console.WriteLine(" ?> Tiling stage done in {0}", sw.Elapsed);
            }
            catch (Exception ex)
            {
                Console.WriteLine(" !> Exception: {0}", ex.Message);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine(" => Pipeline completed in {0}", swg.Elapsed);

                var tmpFolder = Path.Combine(opts.Output, ".temp");

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

                    if (Directory.Exists(tmpFolder))
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