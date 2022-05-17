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

            if (opts.Auto)
                Console.WriteLine(" ?> Auto is not supported yet, using default values");

            Directory.CreateDirectory(opts.Output);

            var pipelineId = Guid.NewGuid().ToString();
            var sw = new Stopwatch();
            var swg = new Stopwatch();
            swg.Start();

            Func<string, string> createTempFolder = opts.UseSystemTempFolder
                ? s => CreateTempFolder(s, Path.GetTempPath())
                : s => CreateTempFolder(s, opts.Output);

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

                var boundsMapper = await StagesFacade.Split(decimateRes.DestFiles, destFolderSplit, opts.Divisions,
                    opts.ZSplit,
                    decimateRes.Bounds);

                Console.WriteLine(" ?> Splitting stage done in {0}", sw.Elapsed);

                if (opts.StopAt == Stage.Splitting)
                    return;

                var gpsCoords = (opts.Latitude != null && opts.Longitude != null && opts.Altitude != null)
                    ? new GpsCoords(opts.Latitude.Value, opts.Longitude.Value, opts.Altitude.Value)
                    : null;

                Console.WriteLine();
                Console.WriteLine($" => Tiling stage {(gpsCoords != null ? $"with GPS coords {gpsCoords}" : "")}");

                sw.Restart();

                StagesFacade.Tile(destFolderSplit, opts.Output, opts.LODs, boundsMapper, gpsCoords);

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

                Console.WriteLine(" => Cleaning up");

                if (destFolderDecimation != null && destFolderDecimation != opts.Output)
                    Directory.Delete(destFolderDecimation, true);

                if (destFolderSplit != null && destFolderSplit != opts.Output)
                    Directory.Delete(destFolderSplit, true);

                Console.WriteLine(" ?> Cleaning up ok");
            }
        }


        private static string CreateTempFolder(string folderName, string baseFolder)
        {
            var tempFolder = Path.Combine(baseFolder, folderName);
            Directory.CreateDirectory(tempFolder);
            return tempFolder;
        }
    }
}