using System.Collections.Generic;
using NUnit.Framework;
using Obj2Tiles.Library.Algos;
using Obj2Tiles.Library.Algos.Model;
using Shouldly;

namespace Obj2Tiles.Library.Test;

/// <summary>
/// Tests for the MaxRects bin packer, focusing on the batch global-best-fit <c>Insert</c> overload
/// added from juj/RectangleBinPack. The batch variant must never pack less densely than inserting one
/// rectangle at a time.
/// </summary>
public class MaxRectanglesBinPackTests
{
    private const FreeRectangleChoiceHeuristic Heuristic = FreeRectangleChoiceHeuristic.RectangleBestAreaFit;

    /// <summary>Deterministic mix of rectangle sizes that stresses the packer.</summary>
    private static (int Width, int Height)[] SampleRects()
    {
        var rects = new List<(int, int)>();
        var sizes = new[] { 200, 120, 90, 60, 40, 30 };
        for (var i = 0; i < 40; i++)
            rects.Add((sizes[i % sizes.Length] + (i % 7) * 5, sizes[(i + 3) % sizes.Length] + (i % 5) * 5));
        return rects.ToArray();
    }

    private static int PlacedCount(Rectangle?[] placements)
    {
        var n = 0;
        foreach (var p in placements)
            if (p != null) n++;
        return n;
    }

    [Test]
    public void BatchInsert_PlacesAllRects_WhenTheyFit()
    {
        var rects = SampleRects();
        var bin = new MaxRectanglesBinPack(1024, 1024, false);

        var placements = bin.Insert(rects, Heuristic);

        placements.Length.ShouldBe(rects.Length);
        placements.ShouldAllBe(p => p != null);
    }

    [Test]
    public void BatchInsert_PlacedRectsMatchRequestedSizes()
    {
        var rects = SampleRects();
        var bin = new MaxRectanglesBinPack(1024, 1024, false);

        var placements = bin.Insert(rects, Heuristic);

        for (var i = 0; i < rects.Length; i++)
        {
            placements[i].ShouldNotBeNull();
            // No rotation requested: the placed size must equal the requested size.
            placements[i]!.Width.ShouldBe(rects[i].Width);
            placements[i]!.Height.ShouldBe(rects[i].Height);
        }
    }

    [Test]
    public void BatchInsert_PlacedRectsDoNotOverlapAndStayInBounds()
    {
        var rects = SampleRects();
        var bin = new MaxRectanglesBinPack(1024, 1024, false);

        var placements = bin.Insert(rects, Heuristic);

        foreach (var p in placements)
        {
            p.ShouldNotBeNull();
            p!.X.ShouldBeGreaterThanOrEqualTo(0);
            p.Y.ShouldBeGreaterThanOrEqualTo(0);
            (p.X + p.Width).ShouldBeLessThanOrEqualTo(1024);
            (p.Y + p.Height).ShouldBeLessThanOrEqualTo(1024);
        }

        // No two placements may overlap.
        for (var i = 0; i < placements.Length; i++)
        for (var j = i + 1; j < placements.Length; j++)
        {
            var a = placements[i]!;
            var b = placements[j]!;
            var disjoint = a.X + a.Width <= b.X || b.X + b.Width <= a.X ||
                           a.Y + a.Height <= b.Y || b.Y + b.Height <= a.Y;
            disjoint.ShouldBeTrue($"placements {i} and {j} overlap");
        }
    }

    [Test]
    public void BatchInsert_ReturnsNull_ForRectsThatDoNotFit()
    {
        var rects = new (int, int)[] { (100, 100), (2000, 100) };
        var bin = new MaxRectanglesBinPack(512, 512, false);

        var placements = bin.Insert(rects, Heuristic);

        placements[0].ShouldNotBeNull();
        placements[1].ShouldBeNull();
    }

    [Test]
    public void BatchInsert_IsNeverLessDenseThanSequential_OnTightBin()
    {
        var rects = SampleRects();

        // A bin too small to hold every rectangle, so packing quality decides how many fit.
        const int edge = 640;

        // Sequential: insert one at a time in the given order.
        var seqBin = new MaxRectanglesBinPack(edge, edge, false);
        foreach (var (w, h) in rects)
            seqBin.Insert(w, h, Heuristic);
        var seqOcc = seqBin.Occupancy();

        // Batch: global best-fit selection.
        var batchBin = new MaxRectanglesBinPack(edge, edge, false);
        var batchPlacements = batchBin.Insert(rects, Heuristic);
        var batchOcc = batchBin.Occupancy();

        // The batch packer must never achieve lower occupancy than the sequential one.
        batchOcc.ShouldBeGreaterThanOrEqualTo(seqOcc);
        PlacedCount(batchPlacements).ShouldBeGreaterThan(0);
    }
}
