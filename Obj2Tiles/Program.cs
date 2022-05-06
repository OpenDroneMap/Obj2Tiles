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
            var mesh = MeshUtils.LoadMesh(opts.Input);

            Console.WriteLine(" ?> Loaded {0} vertices, {1} faces in {2}ms", mesh.VertexCount, mesh.FacesCount,
                sw.ElapsedMilliseconds);

            Console.WriteLine(
                $" -> Splitting with a depth of {opts.Divisions}" + (opts.ZSplit ? " with z-split" : ""));

            var meshes = new ConcurrentBag<IMesh>();

            //Common.Epsilon = 0.00001f;
            
            sw.Restart();

            var count = opts.ZSplit
                ? await MeshUtils.RecurseSplitXYZ(mesh, opts.Divisions, meshes)
                : await MeshUtils.RecurseSplitXY(mesh, opts.Divisions, meshes);

            sw.Stop();

            Console.WriteLine(
                $" ?> Done {count} edge splits in {sw.ElapsedMilliseconds}ms ({(double)count / sw.ElapsedMilliseconds:F2} split/ms)");

            Console.WriteLine(" -> Writing tiles");

            Directory.CreateDirectory(opts.Output);

            sw.Restart();

            var ms = meshes.ToArray();
            for (var index = 0; index < ms.Length; index++)
            {
                var m = ms[index];

                if (m is MeshT t)
                    t.KeepOriginalTextures = opts.KeepOriginalTextures;
                
                m.WriteObj(Path.Combine(opts.Output, $"{m.Name}.obj"));
            }

            Console.WriteLine($" ?> {meshes.Count} tiles written in {sw.ElapsedMilliseconds}ms");
        }
    }
}