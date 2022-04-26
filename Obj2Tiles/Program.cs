using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using CommandLine;
using CommandLine.Text;
using Obj2Tiles.Library;
using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles
{
    internal class Program
    {
        public class Options
        {
            [Option('i', "input", Required = true, HelpText = "Input OBJ file.")]
            public string Input { get; set; }

            [Option('o', "output", Required = true, HelpText = "Output file / folder.")]
            public string Output { get; set; }

            [Option('s', "stage", Required = false, HelpText = "Stage to stop at.", Default = Stage.Tiles)]
            public Stage StopAt { get; set; }

            [Option('d', "divisions", Required = false, HelpText = "How many tiles divisions", Default = 2)]
            public int Divisions { get; set; }

            [Option('z', "zsplit", Required = false, HelpText = "Adds split along z axis", Default = false)]
            public bool ZSplit { get; set; }
        }

        public enum Stage
        {
            Splitting,
            Decimation,
            Tiles
        }

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

            var sw = new Stopwatch();

            Console.WriteLine($" -> Loading OBJ file \"{opts.Input}\"");

            sw.Start();
            var mesh = new Mesh3D(opts.Input);

            Console.WriteLine(" ?> Loaded {0} vertices, {1} faces in {2}ms", mesh.Vertices.Count, mesh.Faces.Count,
                sw.ElapsedMilliseconds);

            Console.WriteLine(
                $" -> Splitting with a depth of {opts.Divisions}" + (opts.ZSplit ? " with z-split" : ""));

            var meshes = new ConcurrentBag<Mesh3D>();

            sw.Restart();

            var count = opts.ZSplit
                ? await Mesh3D.RecurseSplitXYZ(mesh, opts.Divisions, meshes)
                : await Mesh3D.RecurseSplitXY(mesh, opts.Divisions, meshes);

            sw.Stop();

            Console.WriteLine(
                $" ?> Done {count} edge splits in {sw.ElapsedMilliseconds}ms ({(double)count / sw.ElapsedMilliseconds:F2} split/ms)");

            Console.WriteLine(" -> Writing tiles");

            sw.Restart();

            var ms = meshes.ToArray();
            for (var index = 0; index < ms.Length; index++)
            {
                var m = ms[index];
                m.WriteObj(Path.Combine(opts.Output, $"{index}-{m.Name}.obj"));
            }

            Console.WriteLine($" ?> {meshes.Count} tiles written in {sw.ElapsedMilliseconds}ms");
        }
    }
}