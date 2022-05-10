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

                    var res = await StagesFacade.Decimate(opts.Input, tempFolderDecimation, opts.LODs);

                    Console.WriteLine(" ?> Decimation stage done in {0}", sw.Elapsed);
                    Console.WriteLine(" -> Copying obj dependencies");

                    sw.Restart();
                    CopyObjDependencies(opts.Input, tempFolderDecimation);
                    Console.WriteLine(" ?> Dependencies copied in {0}", sw.Elapsed);

                    Console.WriteLine();
                    Console.WriteLine(" => Splitting stage");

                    //var sw2 = Stopwatch.StartNew();

                    tasks = new List<Task>();

                    for (var index = 0; index < res.DestFiles.Length; index++)
                    {
                        var file = res.DestFiles[index];

                        // We compress textures except the first one (the original one)
                        var splitTask = StagesFacade.Split(file, Path.Combine(opts.Output, "LOD-" + index),
                            opts.Divisions, opts.ZSplit, res.Bounds,
                            index == 0 ? TexturesStrategy.Repack : TexturesStrategy.RepackCompressed);

                        tasks.Add(splitTask);
                    }

                    await Task.WhenAll(tasks);

                    Console.WriteLine(" ?> Splitting stage done in {0}", sw.Elapsed);

                    Directory.Delete(tempFolderDecimation, true);

                    break;
                /*
                  case Stage.Conversion:
                      
                      
                      tempFolderDecimation = CreateTempFolder($"{pipelineId}-obj2tiles-decimation");
  
                      destFiles =  await StagesFacade.Decimate(opts.Input, tempFolderDecimation, opts.LODs);
                      
                      CopyObjDependencies(opts.Input, tempFolderDecimation);
                      
                      tempFolderSplit = CreateTempFolder($"{pipelineId}-obj2tiles-split");
  
                      tasks = new List<Task>();
                      
                      for (var index = 0; index < destFiles.Length; index++)
                      {
                          var file = destFiles[index];
                          var splitStage = new SplitStage(file, Path.Combine(tempFolderSplit, "LOD-" + index), opts.Divisions, opts.ZSplit,
                              opts.KeepOriginalTextures);
  
                          tasks.Add(splitStage.Run());
                      }
                      
                      await Task.WhenAll(tasks);
  
                      Directory.Delete(tempFolderDecimation, true);
  
                      var conversionStage = new ConversionStage(tempFolderSplit, opts.Output);
                      await conversionStage.Run();
                      
                      Directory.Delete(tempFolderDecimation, true);
                      
                      break;
                  
                  /*
                  case Stage.Tiling:
  
                      tempFolderSplit = CreateTempFolder($"{pipelineId}-obj2tiles-split");
  
                      stages.Add(new SplitStage(opts.Input, tempFolderSplit, opts.Divisions, opts.ZSplit,
                          opts.KeepOriginalTextures));
                      
                      tempFolderDecimation = CreateTempFolder($"{pipelineId}-obj2tiles-decimation");
  
                      stages.Add(new DecimationStage(tempFolderSplit, tempFolderDecimation));
                      stages.Add(new CleanupStage(Array.Empty<string>(), new[] { tempFolderSplit }));
  
                      var tempFolderConversion = CreateTempFolder($"{pipelineId}-obj2tiles-conversion");
                      
                      stages.Add(new ConversionStage(tempFolderDecimation, tempFolderConversion));
                      stages.Add(new CleanupStage(Array.Empty<string>(), new[] { tempFolderDecimation }));
                      
                      stages.Add(new TilingStage(tempFolderConversion, opts.Output));
                      stages.Add(new CleanupStage(Array.Empty<string>(), new[] { tempFolderConversion }));
  
                      break;*/
                default:
                    throw new ArgumentOutOfRangeException();
            }
            /*
            var sw = new Stopwatch();
            

            foreach (var stage in stages)
            {
                Console.WriteLine(" => Running stage " + stage.GetType().Name);
                sw.Restart();
                await stage.Run();
                Console.WriteLine($" ?> Finished stage {stage.GetType().Name} in {sw.Elapsed}");
                Console.WriteLine();
            }*/
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