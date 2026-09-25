using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Stages;
using Obj2Tiles.Stages.Model;
using Shouldly;

namespace Obj2Tiles.Test;

public class GeometricErrorTests
{
    private const string TestOutputPath = "TestOutput";

    // Three LODs whose middle LOD reports a larger average edge than its parent, so per-tile
    // estimation alone would yield a child error above the parent's.
    private static readonly (string Name, TileBounds Bounds)[] OctreeLods =
    [
        ("Mesh-XL-YL-XL", new TileBounds(new Box3(0, 0, 0, 1, 1, 1), 0.1, 0.2, 10)),
        ("Mesh-XL-YL", new TileBounds(new Box3(0, 0, 0, 2, 2, 2), 5, 6, 10)),
        ("Mesh-XL", new TileBounds(new Box3(0, 0, 0, 4, 4, 4), 1, 2, 10))
    ];

    private static string PrepareSource(string testName, bool octree)
    {
        var folder = Path.Combine(TestOutputPath, testName);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);

        for (var lod = 0; lod < OctreeLods.Length; lod++)
        {
            var dir = Path.Combine(folder, "src", $"LOD-{lod}");
            Directory.CreateDirectory(dir);
            var name = octree ? OctreeLods[lod].Name : "Mesh";
            File.WriteAllText(Path.Combine(dir, name + ".obj"), "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        }

        return folder;
    }

    private static Dictionary<string, TileBounds>[] BoundsMapper(bool octree) =>
        OctreeLods.Select(l => new Dictionary<string, TileBounds> { [octree ? l.Name : "Mesh"] = l.Bounds }).ToArray();

    private static Tileset RunTile(string testName, bool octree, ErrorEstimationMode mode, double lodTextureScale,
        double? baseError = null)
    {
        var folder = PrepareSource(testName, octree);
        StagesFacade.Tile(Path.Combine(folder, "src"), folder, OctreeLods.Length, baseError, BoundsMapper(octree),
            localMode: true, isOctree: octree, errorEstimationMode: mode, lodTextureScale: lodTextureScale);

        return JsonConvert.DeserializeObject<Tileset>(File.ReadAllText(Path.Combine(folder, "tileset.json")))!;
    }

    private static IEnumerable<(TileElement Parent, TileElement Child)> Edges(TileElement tile)
    {
        foreach (var child in tile.Children ?? [])
        {
            yield return (tile, child);
            foreach (var edge in Edges(child))
                yield return edge;
        }
    }

    private static IEnumerable<TestCaseData> AllModes()
    {
        foreach (var mode in Enum.GetValues<ErrorEstimationMode>())
        foreach (var octree in new[] { true, false })
        foreach (var scale in new[] { 0.5, 1.0 })
            yield return new TestCaseData(mode, octree, scale).SetName($"ChildErrorNeverExceedsParent({mode},octree={octree},scale={scale.ToString(CultureInfo.InvariantCulture)})");
    }

    [TestCaseSource(nameof(AllModes))]
    public void ChildErrorNeverExceedsParent(ErrorEstimationMode mode, bool octree, double scale)
    {
        var tileset = RunTile($"GE_{mode}_{octree}_{scale}", octree, mode, scale);

        tileset.Root!.GeometricError.ShouldBeGreaterThan(0);
        tileset.GeometricError.ShouldBeGreaterThanOrEqualTo(tileset.Root.GeometricError);

        var edges = Edges(tileset.Root).ToList();
        edges.Count.ShouldBe(OctreeLods.Length);
        foreach (var (parent, child) in edges)
            child.GeometricError.ShouldBeLessThanOrEqualTo(parent.GeometricError,
                $"{child.Content?.Uri} has a larger error than its parent {parent.Content?.Uri ?? "root"}");
    }

    [Test]
    public void ToplevelModes_HalveOncePerLevel()
    {
        var tileset = RunTile(nameof(ToplevelModes_HalveOncePerLevel), true,
            ErrorEstimationMode.ToplevelAverageEdgeLength, 0.5);

        var root = tileset.Root!;
        var lod2 = root.Children!.Single();
        var lod1 = lod2.Children!.Single();
        var lod0 = lod1.Children!.Single();

        lod2.GeometricError.ShouldBe(root.GeometricError / 2, 1e-12);
        lod1.GeometricError.ShouldBe(root.GeometricError / 4, 1e-12);
        lod0.GeometricError.ShouldBe(0);
    }

    [Test]
    public void ZeroBaseError_IsAutoComputed()
    {
        var tileset = RunTile(nameof(ZeroBaseError_IsAutoComputed), true, ErrorEstimationMode.AverageEdgeLength, 0.5,
            baseError: 0);

        tileset.Root!.GeometricError.ShouldBeGreaterThan(0);
    }

    [Test]
    public void ExplicitBaseError_IsUsedForRoot()
    {
        var tileset = RunTile(nameof(ExplicitBaseError_IsUsedForRoot), true, ErrorEstimationMode.AverageEdgeLength, 0.5,
            baseError: 7);

        tileset.Root!.GeometricError.ShouldBe(7);
        tileset.GeometricError.ShouldBe(7);
    }
}
