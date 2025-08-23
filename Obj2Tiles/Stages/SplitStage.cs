using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles.Stages;

public static partial class StagesFacade
{
    public static async Task<Dictionary<string, Box3>[]> Split(string[] sourceFiles, string destFolder, int divisions,
        bool zsplit, Box3 bounds, bool keepOriginalTextures = false)
    {
      
        var tasks = new List<Task<Dictionary<string, Box3>>>();

        for (var index = 0; index < sourceFiles.Length; index++)
        {
            var file = sourceFiles[index];
            var dest = Path.Combine(destFolder, "LOD-" + index);
            
            // We compress textures except the first one (the original one)
            var textureStrategy = keepOriginalTextures ? TexturesStrategy.KeepOriginal :
                index == 0 ? TexturesStrategy.Repack : TexturesStrategy.RepackCompressed;

            var splitTask = Split(file, dest, divisions, zsplit, bounds, textureStrategy);

            tasks.Add(splitTask);
        }

        await Task.WhenAll(tasks);

        return tasks.Select(task => task.Result).ToArray();
    }

    public static async Task<Dictionary<string, Box3>> Split(string sourcePath, string destPath, int divisions,
        bool zSplit = false,
        Box3? bounds = null,
        TexturesStrategy textureStrategy = TexturesStrategy.Repack,
        SplitPointStrategy splitPointStrategy = SplitPointStrategy.VertexBaricenter)
    {
        var sw = new Stopwatch();
        var tilesBounds = new Dictionary<string, Box3>();

        Directory.CreateDirectory(destPath);
        
        Console.WriteLine($" -> Loading OBJ file \"{sourcePath}\"");

        sw.Start();
        var mesh = MeshUtils.LoadMesh(sourcePath, out var deps);

        Console.WriteLine(
            $" ?> Loaded {mesh.VertexCount} vertices, {mesh.FacesCount} faces in {sw.ElapsedMilliseconds}ms");

        if (divisions == 0)
        {
            Console.WriteLine(" -> Skipping split stage, just compressing textures and cleaning up the mesh");

            if (mesh is MeshT t)
                t.TexturesStrategy = TexturesStrategy.Compress;
            
            mesh.WriteObj(Path.Combine(destPath, $"{mesh.Name}.obj"));
            
            return new Dictionary<string, Box3> { { mesh.Name, mesh.Bounds } };
            
        }
                
        Console.WriteLine(
            $" -> Splitting with a depth of {divisions}{(zSplit ? " with z-split" : "")}");

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
                _ => throw new ArgumentOutOfRangeException(nameof(splitPointStrategy))
            };

            count = zSplit
                ? await MeshUtils.RecurseSplitXYZ(mesh, divisions, getSplitPoint, meshes)
                : await MeshUtils.RecurseSplitXY(mesh, divisions, getSplitPoint, meshes);
        }

        sw.Stop();

        Console.WriteLine(
            $" ?> Done {count} edge splits in {sw.ElapsedMilliseconds}ms ({(double)count / sw.ElapsedMilliseconds:F2} split/ms)");

        Console.WriteLine(" -> Writing tiles");

        sw.Restart();

        var ms = meshes.ToArray();
        foreach (var m in ms)
        {
            if (m is MeshT t)
                t.TexturesStrategy = textureStrategy;

            m.WriteObj(Path.Combine(destPath, $"{m.Name}.obj"));

            tilesBounds.Add(m.Name, m.Bounds);
        }

        Console.WriteLine($" ?> {meshes.Count} tiles written in {sw.ElapsedMilliseconds}ms");

        return tilesBounds;
    }
}

public enum SplitPointStrategy
{
    AbsoluteCenter,
    VertexBaricenter
}