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

            var stages = new List<IStage>();
            
            switch (opts.StopAt)
            {
                case Stage.Splitting:
                    
                    stages.Add(new SplitStage(opts.Input, opts.Output, opts.Divisions, opts.ZSplit,
                        opts.KeepOriginalTextures));
                    
                    break;
                case Stage.Decimation:
                    
                    var tempFolder = Path.Combine(Path.GetTempPath(), "obj2tiles-" + Guid.NewGuid());
                    Directory.CreateDirectory(tempFolder);
                    
                    stages.Add(new SplitStage(opts.Input, tempFolder, opts.Divisions, opts.ZSplit,
                        opts.KeepOriginalTextures));
                    
                    //stages.Add(new DecimationStage(tempFolder, opts.Output));
                    
                    Console.WriteLine(" !> Decimation stage not yet implemented");
                    
                    break;
                case Stage.Tiles:
                    
                    stages.Add(new SplitStage(opts.Input, opts.Output, opts.Divisions, opts.ZSplit,
                        opts.KeepOriginalTextures));
                    
                    Console.WriteLine(" !> Decimation stage not yet implemented");
                    Console.WriteLine(" !> Tiling stage not yet implemented");

                    
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            foreach (var stage in stages)
            {
                Console.WriteLine(" -> Running stage " + stage.GetType().Name);
                await stage.Run();
            }

        }
    }
}