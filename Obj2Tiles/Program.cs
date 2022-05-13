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
            {
                Console.WriteLine(" ?> Auto is not supported yet, using default values");
            }

            string? tempFolderSplit;
            string? tempFolderDecimation;

            Directory.CreateDirectory(opts.Output);

            var pipelineId = Guid.NewGuid().ToString();
            var sw = new Stopwatch();

            string[] destFiles;
            List<Task>? tasks;
            DecimateResult? decimateRes;
            switch (opts.StopAt)
            {
                case Stage.Decimation:

                    Console.WriteLine(" => Decimation stage");
                    sw.Start();

                    await StagesFacade.Decimate(opts.Input, opts.Output, opts.LODs);

                    Console.WriteLine(" ?> Decimation stage done in {0}", sw.Elapsed);
                    Console.WriteLine(" -> Copying obj dependencies");
                    
                    sw.Restart();
                    
                    CopyObjDependencies(opts.Input, opts.Output);

                    Console.WriteLine(" ?> Dependencies copied in {0}", sw.Elapsed);

                    break;

                case Stage.Splitting:

                    tempFolderDecimation = CreateTempFolder($"{pipelineId}-obj2tiles-decimation");

                    Console.WriteLine(" => Decimation stage");
                    sw.Start();

                    decimateRes = await StagesFacade.Decimate(opts.Input, tempFolderDecimation, opts.LODs);

                    Console.WriteLine(" ?> Decimation stage done in {0}", sw.Elapsed);
                    Console.WriteLine(" -> Copying obj dependencies");

                    sw.Restart();
                    CopyObjDependencies(opts.Input, tempFolderDecimation);
                    Console.WriteLine(" ?> Dependencies copied in {0}", sw.Elapsed);

                    Console.WriteLine();
                    Console.WriteLine(" => Splitting stage");

                    //var sw2 = Stopwatch.StartNew();

                    tasks = new List<Task>();

                    for (var index = 0; index < decimateRes.DestFiles.Length; index++)
                    {
                        var file = decimateRes.DestFiles[index];

                        // We compress textures except the first one (the original one)
                        var splitTask = StagesFacade.Split(file, Path.Combine(opts.Output, "LOD-" + index),
                            opts.Divisions, opts.ZSplit, decimateRes.Bounds,
                            index == 0 ? TexturesStrategy.Repack : TexturesStrategy.RepackCompressed);

                        tasks.Add(splitTask);
                    }

                    await Task.WhenAll(tasks);

                    Console.WriteLine(" ?> Splitting stage done in {0}", sw.Elapsed);

                    Directory.Delete(tempFolderDecimation, true);

                    break;
                
                case Stage.Tiling:

                    tempFolderDecimation = CreateTempFolder($"{pipelineId}-obj2tiles-decimation");

                    Console.WriteLine(" => Decimation stage");
                    sw.Start();

                    decimateRes = await StagesFacade.Decimate(opts.Input, tempFolderDecimation, opts.LODs);

                    Console.WriteLine(" ?> Decimation stage done in {0}", sw.Elapsed);
                    Console.WriteLine(" -> Copying obj dependencies");

                    sw.Restart();
                    CopyObjDependencies(opts.Input, tempFolderDecimation);
                    Console.WriteLine(" ?> Dependencies copied in {0}", sw.Elapsed);

                    Console.WriteLine();
                    Console.WriteLine(" => Splitting stage");

                    tempFolderSplit = CreateTempFolder($"{pipelineId}-obj2tiles-split");
                    
                    tasks = new List<Task>();

                    for (var index = 0; index < decimateRes.DestFiles.Length; index++)
                    {
                        var file = decimateRes.DestFiles[index];

                        // We compress textures except the first one (the original one)
                        var splitTask = StagesFacade.Split(file, Path.Combine(tempFolderSplit, "LOD-" + index),
                            opts.Divisions, opts.ZSplit, decimateRes.Bounds,
                            index == 0 ? TexturesStrategy.Repack : TexturesStrategy.RepackCompressed);

                        tasks.Add(splitTask);
                    }

                    await Task.WhenAll(tasks);

                    Console.WriteLine(" ?> Splitting stage done in {0}", sw.Elapsed);
                    
                    Directory.Delete(tempFolderDecimation, true);

                    Console.WriteLine();
                    Console.WriteLine(" => Tiling stage");
                    sw.Restart();
                    
                    StagesFacade.Tile(tempFolderSplit, opts.Output, opts.LODs);

                    Console.WriteLine(" ?> Tiling stage done in {0}", sw.Elapsed);
                    
                    Directory.Delete(tempFolderSplit, true);
                    
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }

        private static void CopyObjDependencies(string input, string output)
        {
            var dependencies = Utils.GetObjDependencies(input);

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
                {
                    continue;
                }

                File.Copy(Path.Combine(Path.GetDirectoryName(input), dependency), dependencyDestPath, true);

                Console.WriteLine($" -> Copied {dependency}");
            }
        }

        private static string CreateTempFolder(string folderName)
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), folderName);
            Directory.CreateDirectory(tempFolder);
            return tempFolder;
        }
    }
}