using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles.Stages;

public class SplitStage : IStage
{

    public readonly string SourcePath;
    public readonly string DestPath;
    public readonly bool ZSplit;
    public readonly int Divisions;
    public readonly bool KeepOriginalTextures;

    public SplitStage(string sourcePath, string destPath, int divisions, bool zSplit, bool keepOriginalTextures)
    {
        SourcePath = sourcePath;
        DestPath = destPath;
        Divisions = divisions;
        ZSplit = zSplit;
        KeepOriginalTextures = keepOriginalTextures;
    }

    public async Task Run()
    {
        var sw = new Stopwatch();

        Console.WriteLine($" -> Loading OBJ file \"{SourcePath}\"");

        sw.Start();
        var mesh = MeshUtils.LoadMesh(SourcePath);

        Console.WriteLine(" ?> Loaded {0} vertices, {1} faces in {2}ms", mesh.VertexCount, mesh.FacesCount,
            sw.ElapsedMilliseconds);

        Console.WriteLine(
            $" -> Splitting with a depth of {Divisions}" + (ZSplit ? " with z-split" : ""));

        var meshes = new ConcurrentBag<IMesh>();
            
        sw.Restart();

        var count = ZSplit
            ? await MeshUtils.RecurseSplitXYZ(mesh, Divisions, meshes)
            : await MeshUtils.RecurseSplitXY(mesh, Divisions, meshes);

        sw.Stop();

        Console.WriteLine(
            $" ?> Done {count} edge splits in {sw.ElapsedMilliseconds}ms ({(double)count / sw.ElapsedMilliseconds:F2} split/ms)");

        Console.WriteLine(" -> Writing tiles");

        Directory.CreateDirectory(DestPath);

        sw.Restart();

        var indented = new JsonSerializerOptions { WriteIndented = true };

        var ms = meshes.ToArray();
        for (var index = 0; index < ms.Length; index++)
        {
            var m = ms[index];

            if (m is MeshT t)
                t.KeepOriginalTextures = KeepOriginalTextures;
                
            m.WriteObj(Path.Combine(DestPath, $"{m.Name}.obj"));
            
            await File.WriteAllTextAsync(Path.Combine(DestPath, $"{m.Name}.json"), JsonSerializer.Serialize(m.Bounds, indented));
            
        }

        Console.WriteLine($" ?> {meshes.Count} tiles written in {sw.ElapsedMilliseconds}ms");    }
}