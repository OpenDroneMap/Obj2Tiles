using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles.Stages;

public static partial class StagesFacade
{
    public static async Task<Dictionary<string, Box3>[]> Split(string[] sourceFiles, string destFolder, int divisions,
        bool zsplit, Box3 bounds, bool keepOriginalTextures = false, SplitPointStrategy splitPointStrategy = SplitPointStrategy.VertexBaricenter)
    {
        var results = new Dictionary<string, Box3>[sourceFiles.Length];

        // Pre-compute split plan from LOD-0 vertices (lightweight — no mesh splitting, just vertex partitioning)
        Console.WriteLine(" -> Pre-computing split plan from LOD-0 vertices");
        var sw = Stopwatch.StartNew();

        var mesh0 = MeshUtils.LoadMesh(sourceFiles[0], out _);
        var vertices0 = mesh0.Vertices.ToArray();

        Func<Vertex3[], Vertex3> computeCenter = splitPointStrategy switch
        {
            SplitPointStrategy.AbsoluteCenter => ComputeBoundsCenter,
            SplitPointStrategy.VertexBaricenter => ComputeBaricenter,
            SplitPointStrategy.VertexMedian => ComputeMedian,
            _ => throw new ArgumentOutOfRangeException(nameof(splitPointStrategy))
        };

        var splitPlan = new Dictionary<string, Vertex3>();
        if (splitPointStrategy == SplitPointStrategy.VertexMedian)
        {
            if (zsplit)
                PreComputeSplitPlanXYZBalanced(vertices0, "Mesh", divisions, computeCenter, splitPlan);
            else
                PreComputeSplitPlanXYBalanced(vertices0, "Mesh", divisions, computeCenter, splitPlan);
        }
        else
        {
            if (zsplit)
                PreComputeSplitPlanXYZ(vertices0, "Mesh", divisions, computeCenter, splitPlan);
            else
                PreComputeSplitPlanXY(vertices0, "Mesh", divisions, computeCenter, splitPlan);
        }

        sw.Stop();
        Console.WriteLine($" ?> Split plan computed: {splitPlan.Count} split points in {sw.ElapsedMilliseconds}ms");

        // Replay function — all LODs use the same pre-computed split points
        Func<IMesh, Vertex3> baseSplitPoint = splitPointStrategy switch
        {
            SplitPointStrategy.AbsoluteCenter => m => m.Bounds.Center,
            SplitPointStrategy.VertexBaricenter => m => m.GetVertexBaricenter(),
            SplitPointStrategy.VertexMedian => m => m.GetVertexMedian(),
            _ => throw new ArgumentOutOfRangeException(nameof(splitPointStrategy))
        };

        Func<IMesh, Vertex3> replaySplitPoint = m =>
            splitPlan.TryGetValue(m.Name, out var pt) ? pt : baseSplitPoint(m);

        // Split all LODs in parallel using the pre-computed split plan
        var tasks = new List<Task<Dictionary<string, Box3>>>();
        for (var index = 0; index < sourceFiles.Length; index++)
        {
            var file = sourceFiles[index];
            var dest = Path.Combine(destFolder, "LOD-" + index);

            var textureStrategy = keepOriginalTextures ? TexturesStrategy.KeepOriginal :
                index == 0 ? TexturesStrategy.Repack : TexturesStrategy.RepackCompressed;

            tasks.Add(Split(file, dest, divisions, zsplit, textureStrategy, splitPointStrategy, replaySplitPoint));
        }

        await Task.WhenAll(tasks);

        for (var i = 0; i < tasks.Count; i++)
            results[i] = tasks[i].Result;

        return results;
    }

    public static async Task<Dictionary<string, Box3>> Split(string sourcePath, string destPath, int divisions,
        bool zSplit = false,
        Box3? bounds = null,
        TexturesStrategy textureStrategy = TexturesStrategy.Repack,
        SplitPointStrategy splitPointStrategy = SplitPointStrategy.VertexBaricenter)
    {
        Func<IMesh, Vertex3> getSplitPoint = splitPointStrategy switch
        {
            SplitPointStrategy.AbsoluteCenter => m => m.Bounds.Center,
            SplitPointStrategy.VertexBaricenter => m => m.GetVertexBaricenter(),
            SplitPointStrategy.VertexMedian => m => m.GetVertexMedian(),
            _ => throw new ArgumentOutOfRangeException(nameof(splitPointStrategy))
        };

        return await Split(sourcePath, destPath, divisions, zSplit, textureStrategy, splitPointStrategy, getSplitPoint);
    }

    private static async Task<Dictionary<string, Box3>> Split(string sourcePath, string destPath, int divisions,
        bool zSplit,
        TexturesStrategy textureStrategy,
        SplitPointStrategy splitPointStrategy,
        Func<IMesh, Vertex3> getSplitPoint)
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

        if (splitPointStrategy == SplitPointStrategy.VertexMedian)
        {
            count = zSplit
                ? await MeshUtils.RecurseSplitXYZBalanced(mesh, divisions, getSplitPoint, meshes)
                : await MeshUtils.RecurseSplitXYBalanced(mesh, divisions, getSplitPoint, meshes);
        }
        else
        {
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

    #region Split Plan Pre-computation (vertex-only, no mesh splitting)

    private static Vertex3 ComputeBoundsCenter(Vertex3[] verts)
    {
        var minX = double.MaxValue; var minY = double.MaxValue; var minZ = double.MaxValue;
        var maxX = double.MinValue; var maxY = double.MinValue; var maxZ = double.MinValue;
        for (var i = 0; i < verts.Length; i++)
        {
            var v = verts[i];
            if (v.X < minX) minX = v.X; if (v.X > maxX) maxX = v.X;
            if (v.Y < minY) minY = v.Y; if (v.Y > maxY) maxY = v.Y;
            if (v.Z < minZ) minZ = v.Z; if (v.Z > maxZ) maxZ = v.Z;
        }
        return new Vertex3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
    }

    private static Vertex3 ComputeBaricenter(Vertex3[] verts)
    {
        double x = 0, y = 0, z = 0;
        for (var i = 0; i < verts.Length; i++)
        {
            x += verts[i].X; y += verts[i].Y; z += verts[i].Z;
        }
        var c = verts.Length;
        return new Vertex3(x / c, y / c, z / c);
    }

    private static Vertex3 ComputeMedian(Vertex3[] verts)
    {
        var count = verts.Length;
        var xs = new double[count]; var ys = new double[count]; var zs = new double[count];
        for (var i = 0; i < count; i++)
        {
            xs[i] = verts[i].X; ys[i] = verts[i].Y; zs[i] = verts[i].Z;
        }
        Array.Sort(xs); Array.Sort(ys); Array.Sort(zs);
        var mid = count / 2;
        return new Vertex3(xs[mid], ys[mid], zs[mid]);
    }

    private static (Vertex3[] left, Vertex3[] right) PartitionX(Vertex3[] verts, double x)
    {
        var left = new List<Vertex3>(verts.Length / 2);
        var right = new List<Vertex3>(verts.Length / 2);
        for (var i = 0; i < verts.Length; i++)
        {
            if (verts[i].X < x) left.Add(verts[i]); else right.Add(verts[i]);
        }
        return (left.ToArray(), right.ToArray());
    }

    private static (Vertex3[] left, Vertex3[] right) PartitionY(Vertex3[] verts, double y)
    {
        var left = new List<Vertex3>(verts.Length / 2);
        var right = new List<Vertex3>(verts.Length / 2);
        for (var i = 0; i < verts.Length; i++)
        {
            if (verts[i].Y < y) left.Add(verts[i]); else right.Add(verts[i]);
        }
        return (left.ToArray(), right.ToArray());
    }

    private static (Vertex3[] left, Vertex3[] right) PartitionZ(Vertex3[] verts, double z)
    {
        var left = new List<Vertex3>(verts.Length / 2);
        var right = new List<Vertex3>(verts.Length / 2);
        for (var i = 0; i < verts.Length; i++)
        {
            if (verts[i].Z < z) left.Add(verts[i]); else right.Add(verts[i]);
        }
        return (left.ToArray(), right.ToArray());
    }

    /// <summary>
    /// Non-balanced XY: one center for both axes, then recurse.
    /// Mirrors RecurseSplitXY(mesh, depth, Func, meshes).
    /// </summary>
    private static void PreComputeSplitPlanXY(Vertex3[] verts, string name, int depth,
        Func<Vertex3[], Vertex3> computeCenter, Dictionary<string, Vertex3> plan)
    {
        if (depth == 0 || verts.Length == 0) return;

        var center = computeCenter(verts);
        plan[name] = center;

        var (leftX, rightX) = PartitionX(verts, center.X);
        var (topLeft, bottomLeft) = PartitionY(leftX, center.Y);
        var (topRight, bottomRight) = PartitionY(rightX, center.Y);

        var nextDepth = depth - 1;
        if (topLeft.Length > 0) PreComputeSplitPlanXY(topLeft, $"{name}-XL-YL", nextDepth, computeCenter, plan);
        if (bottomLeft.Length > 0) PreComputeSplitPlanXY(bottomLeft, $"{name}-XL-YR", nextDepth, computeCenter, plan);
        if (topRight.Length > 0) PreComputeSplitPlanXY(topRight, $"{name}-XR-YL", nextDepth, computeCenter, plan);
        if (bottomRight.Length > 0) PreComputeSplitPlanXY(bottomRight, $"{name}-XR-YR", nextDepth, computeCenter, plan);
    }

    /// <summary>
    /// Non-balanced XYZ: one center for all three axes, then recurse.
    /// Mirrors RecurseSplitXYZ(mesh, depth, Func, meshes).
    /// </summary>
    private static void PreComputeSplitPlanXYZ(Vertex3[] verts, string name, int depth,
        Func<Vertex3[], Vertex3> computeCenter, Dictionary<string, Vertex3> plan)
    {
        if (depth == 0 || verts.Length == 0) return;

        var center = computeCenter(verts);
        plan[name] = center;

        var (leftX, rightX) = PartitionX(verts, center.X);
        var (topLeft, bottomLeft) = PartitionY(leftX, center.Y);
        var (topRight, bottomRight) = PartitionY(rightX, center.Y);

        var (tlNear, tlFar) = PartitionZ(topLeft, center.Z);
        var (blNear, blFar) = PartitionZ(bottomLeft, center.Z);
        var (trNear, trFar) = PartitionZ(topRight, center.Z);
        var (brNear, brFar) = PartitionZ(bottomRight, center.Z);

        var nextDepth = depth - 1;
        if (tlNear.Length > 0) PreComputeSplitPlanXYZ(tlNear, $"{name}-XL-YL-ZL", nextDepth, computeCenter, plan);
        if (tlFar.Length > 0) PreComputeSplitPlanXYZ(tlFar, $"{name}-XL-YL-ZR", nextDepth, computeCenter, plan);
        if (blNear.Length > 0) PreComputeSplitPlanXYZ(blNear, $"{name}-XL-YR-ZL", nextDepth, computeCenter, plan);
        if (blFar.Length > 0) PreComputeSplitPlanXYZ(blFar, $"{name}-XL-YR-ZR", nextDepth, computeCenter, plan);
        if (trNear.Length > 0) PreComputeSplitPlanXYZ(trNear, $"{name}-XR-YL-ZL", nextDepth, computeCenter, plan);
        if (trFar.Length > 0) PreComputeSplitPlanXYZ(trFar, $"{name}-XR-YL-ZR", nextDepth, computeCenter, plan);
        if (brNear.Length > 0) PreComputeSplitPlanXYZ(brNear, $"{name}-XR-YR-ZL", nextDepth, computeCenter, plan);
        if (brFar.Length > 0) PreComputeSplitPlanXYZ(brFar, $"{name}-XR-YR-ZR", nextDepth, computeCenter, plan);
    }

    /// <summary>
    /// Balanced XY: recompute split point per sub-partition per axis.
    /// Mirrors RecurseSplitXYBalanced(mesh, depth, Func, meshes).
    /// </summary>
    private static void PreComputeSplitPlanXYBalanced(Vertex3[] verts, string name, int depth,
        Func<Vertex3[], Vertex3> computeCenter, Dictionary<string, Vertex3> plan)
    {
        if (depth == 0 || verts.Length == 0) return;

        var splitX = computeCenter(verts);
        plan[name] = splitX;

        var (leftX, rightX) = PartitionX(verts, splitX.X);

        if (leftX.Length > 0)
        {
            var splitYLeft = computeCenter(leftX);
            plan[$"{name}-XL"] = splitYLeft;
            var (topLeft, bottomLeft) = PartitionY(leftX, splitYLeft.Y);

            if (depth <= 1)
                return; // leaf level, no further recursion

            if (topLeft.Length > 0) PreComputeSplitPlanXYBalanced(topLeft, $"{name}-XL-YL", depth - 1, computeCenter, plan);
            if (bottomLeft.Length > 0) PreComputeSplitPlanXYBalanced(bottomLeft, $"{name}-XL-YR", depth - 1, computeCenter, plan);
        }

        if (rightX.Length > 0)
        {
            var splitYRight = computeCenter(rightX);
            plan[$"{name}-XR"] = splitYRight;
            var (topRight, bottomRight) = PartitionY(rightX, splitYRight.Y);

            if (depth <= 1)
                return;

            if (topRight.Length > 0) PreComputeSplitPlanXYBalanced(topRight, $"{name}-XR-YL", depth - 1, computeCenter, plan);
            if (bottomRight.Length > 0) PreComputeSplitPlanXYBalanced(bottomRight, $"{name}-XR-YR", depth - 1, computeCenter, plan);
        }
    }

    /// <summary>
    /// Balanced XYZ: recompute split point per sub-partition per axis.
    /// Mirrors RecurseSplitXYZBalanced(mesh, depth, Func, meshes).
    /// </summary>
    private static void PreComputeSplitPlanXYZBalanced(Vertex3[] verts, string name, int depth,
        Func<Vertex3[], Vertex3> computeCenter, Dictionary<string, Vertex3> plan)
    {
        if (depth == 0 || verts.Length == 0) return;

        var splitX = computeCenter(verts);
        plan[name] = splitX;

        var (leftX, rightX) = PartitionX(verts, splitX.X);

        var halves = new[] { (leftX, "XL"), (rightX, "XR") };
        var quadrants = new List<(Vertex3[] verts, string name)>();

        foreach (var (half, xLabel) in halves)
        {
            if (half.Length == 0) continue;
            var halfName = $"{name}-{xLabel}";
            var splitY = computeCenter(half);
            plan[halfName] = splitY;

            var (top, bottom) = PartitionY(half, splitY.Y);
            if (top.Length > 0) quadrants.Add((top, $"{halfName}-YL"));
            if (bottom.Length > 0) quadrants.Add((bottom, $"{halfName}-YR"));
        }

        var octants = new List<(Vertex3[] verts, string name)>();
        foreach (var (quad, quadName) in quadrants)
        {
            var splitZ = computeCenter(quad);
            plan[quadName] = splitZ;

            var (near, far) = PartitionZ(quad, splitZ.Z);
            if (near.Length > 0) octants.Add((near, $"{quadName}-ZL"));
            if (far.Length > 0) octants.Add((far, $"{quadName}-ZR"));
        }

        var nextDepth = depth - 1;
        if (nextDepth == 0) return;

        foreach (var (oct, octName) in octants)
            PreComputeSplitPlanXYZBalanced(oct, octName, nextDepth, computeCenter, plan);
    }

    #endregion
}

public enum SplitPointStrategy
{
    AbsoluteCenter,
    VertexBaricenter,
    VertexMedian
}