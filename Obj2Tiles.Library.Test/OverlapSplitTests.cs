using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Shouldly;

namespace Obj2Tiles.Library.Test;

public class OverlapSplitTests
{
    private static Mesh FlatGrid(int cells, double cellSize)
    {
        var vertices = new List<Vertex3>();
        for (var j = 0; j <= cells; j++)
        for (var i = 0; i <= cells; i++)
            vertices.Add(new Vertex3(i * cellSize, j * cellSize, 0));

        var faces = new List<Face>();
        int Index(int i, int j) => j * (cells + 1) + i;
        for (var j = 0; j < cells; j++)
        for (var i = 0; i < cells; i++)
        {
            faces.Add(new Face(Index(i, j), Index(i + 1, j), Index(i + 1, j + 1)));
            faces.Add(new Face(Index(i, j), Index(i + 1, j + 1), Index(i, j + 1)));
        }

        return new Mesh(vertices, faces);
    }

    [Test]
    public async Task RecurseSplitXY_Bounds_WithOverlap_ExtendsLeavesPastSplitPlane()
    {
        var mesh = FlatGrid(10, 1.0);
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXY(mesh, 1, mesh.Bounds, bag, overlap: 0.5);

        var leaves = bag.ToArray();
        leaves.Length.ShouldBe(4);
        leaves.Count(m => m.Bounds.Min.X < 1).ShouldBe(2);
        foreach (var leaf in leaves.Where(m => m.Bounds.Min.X < 1))
            leaf.Bounds.Max.X.ShouldBeGreaterThan(5.4, "left tiles must carry the overlap band past x = 5");
        foreach (var leaf in leaves.Where(m => m.Bounds.Min.X >= 1))
            leaf.Bounds.Min.X.ShouldBeLessThan(4.6, "right tiles must carry the overlap band before x = 5");
    }

    [Test]
    public async Task RecurseSplitXY_Bounds_WithoutOverlap_KeepsExactBoundary()
    {
        var mesh = FlatGrid(10, 1.0);
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXY(mesh, 1, mesh.Bounds, bag);

        foreach (var leaf in bag.Where(m => m.Bounds.Min.X < 1))
            leaf.Bounds.Max.X.ShouldBe(5.0, 1e-9);
    }

    [Test]
    public async Task RecurseSplitXYZ_Bounds_WithOverlap_ExtendsLeavesPastSplitPlane()
    {
        var mesh = FlatGrid(10, 1.0);
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYZ(mesh, 1, mesh.Bounds, bag, overlap: 0.5);

        foreach (var leaf in bag.Where(m => m.Bounds.Min.X < 1))
            leaf.Bounds.Max.X.ShouldBeGreaterThan(5.4);
    }

    [Test]
    public void OverlapNudge_ScalesWithTileSize()
    {
        // 1000 x 1000 tile: the nudge must be allowed to grow well past the old absolute 0.0001 cap.
        var mesh = FlatGrid(10, 100.0);
        var before = mesh.Bounds.Min;
        var diagonal = mesh.Bounds.Diagonal();

        MeshUtils.ApplyOverlapNudge(mesh, overlap: 1.0);

        var shift = mesh.Bounds.Min - before;
        var limit = Math.Min(0.2, MeshUtils.OverlapNudgeTileFraction * diagonal);
        var components = new[] { Math.Abs(shift.X), Math.Abs(shift.Y), Math.Abs(shift.Z) };

        components.Max().ShouldBeLessThanOrEqualTo(limit);
        components.Max().ShouldBeGreaterThan(0.0001);
    }

    [Test]
    public void OverlapNudge_StaysBelowOverlapWidth()
    {
        var mesh = FlatGrid(10, 100.0);
        var before = mesh.Bounds.Min;

        MeshUtils.ApplyOverlapNudge(mesh, overlap: 0.001);

        var shift = mesh.Bounds.Min - before;
        Math.Max(Math.Abs(shift.X), Math.Max(Math.Abs(shift.Y), Math.Abs(shift.Z)))
            .ShouldBeLessThanOrEqualTo(0.2 * 0.001);
    }
}
