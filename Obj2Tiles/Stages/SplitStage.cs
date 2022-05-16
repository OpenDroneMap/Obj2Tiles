using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles.Stages;

public static partial class StagesFacade
{
    public static async Task Split(string sourcePath, string destPath, int divisions, bool zSplit = false,
        Box3? bounds = null,
        TexturesStrategy textureStrategy = TexturesStrategy.Repack, SplitPointStrategy splitPointStrategy = SplitPointStrategy.VertexBaricenter)
    {
        var sw = new Stopwatch();

        Console.WriteLine($" -> Loading OBJ file \"{sourcePath}\"");

        sw.Start();
        var mesh = MeshUtils.LoadMesh(sourcePath);

        Console.WriteLine(" ?> Loaded {0} vertices, {1} faces in {2}ms", mesh.VertexCount, mesh.FacesCount,
            sw.ElapsedMilliseconds);

        Console.WriteLine(
            $" -> Splitting with a depth of {divisions}" + (zSplit ? " with z-split" : ""));

        var meshes = new ConcurrentBag<IMesh>();

        sw.Restart();

        int count;
        
        if (bounds != null)
        {
            count = zSplit
                ? await MeshUtils.RecurseSplitXYZ(mesh, divisions, bounds, meshes)
                : await MeshUtils.RecurseSplitXY(mesh, divisions, bounds, meshes);
        }
        else
        {
            Func<IMesh, Vertex3> getSplitPoint = splitPointStrategy switch
            {
                SplitPointStrategy.AbsoluteCenter => m => m.Bounds.Center,
                SplitPointStrategy.VertexBaricenter => m => m.GetVertexBaricenter(),
                _ => throw new ArgumentOutOfRangeException()
            };

            count = zSplit
                ? await MeshUtils.RecurseSplitXYZ(mesh, divisions, getSplitPoint, meshes)
                : await MeshUtils.RecurseSplitXY(mesh, divisions, getSplitPoint, meshes);
        }

        sw.Stop();

        Console.WriteLine(
            $" ?> Done {count} edge splits in {sw.ElapsedMilliseconds}ms ({(double)count / sw.ElapsedMilliseconds:F2} split/ms)");

        Console.WriteLine(" -> Writing tiles");

        Directory.CreateDirectory(destPath);

        sw.Restart();

        var indented = new JsonSerializerOptions { WriteIndented = true };

        var ms = meshes.ToArray();
        for (var index = 0; index < ms.Length; index++)
        {
            var m = ms[index];

            if (m is MeshT t)
                t.TexturesStrategy = textureStrategy;

            m.WriteObj(Path.Combine(destPath, $"{m.Name}.obj"));

            await File.WriteAllTextAsync(Path.Combine(destPath, $"{m.Name}.json"),
                JsonSerializer.Serialize(m.Bounds, indented));
        }

        Console.WriteLine($" ?> {meshes.Count} tiles written in {sw.ElapsedMilliseconds}ms");
    }
}


public enum SplitPointStrategy
{
    AbsoluteCenter,
    VertexBaricenter
}