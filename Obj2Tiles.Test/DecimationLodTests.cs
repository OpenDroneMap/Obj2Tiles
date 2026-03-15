using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Obj2Tiles.Stages;
using Obj2Tiles.Stages.Model;
using Shouldly;

namespace Obj2Tiles.Test;

/// <summary>
/// Regression tests for the decimation stage.
/// Verifies that successive LODs have monotonically decreasing triangle counts (issue #23).
/// </summary>
public class DecimationLodTests
{
    private const string TestOutputPath = "TestOutput";

    private static string GetTestOutputPath(string testName)
    {
        var folder = Path.Combine(TestOutputPath, testName);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        return folder;
    }

    [SetUp]
    public void Setup()
    {
        Directory.CreateDirectory(TestOutputPath);
    }

    private static int CountTriangles(string objPath)
    {
        var mesh = new ObjMesh();
        mesh.ReadFile(objPath);
        return mesh.SubMeshIndices!.Sum(idx => idx.Length / 3);
    }

    [Test]
    public async Task Decimate_LodTriangleCounts_MonotonicallyDecrease()
    {
        var testPath = GetTestOutputPath(nameof(Decimate_LodTriangleCounts_MonotonicallyDecrease));

        // Use real test mesh (Tile2) which has UV coords and materials
        var srcObj = Path.GetFullPath("TestData/Tile2/Mesh-XL-YR-XR-YL.obj");
        File.Exists(srcObj).ShouldBeTrue($"Test fixture not found: {srcObj}");

        var srcTriangles = CountTriangles(srcObj);
        srcTriangles.ShouldBeGreaterThan(1000, "Source mesh should have enough triangles for meaningful decimation");

        var lodOutputPath = Path.Combine(testPath, "lods");
        Directory.CreateDirectory(lodOutputPath);

        // 3 LODs: LOD-0 is original, LOD-1/2 are decimated at quality 0.66 and 0.33
        var result = await StagesFacade.Decimate(srcObj, lodOutputPath, lods: 3);

        result.DestFiles.Length.ShouldBe(3, "Should produce 3 LOD files (original + 2 decimated)");

        var triCounts = result.DestFiles.Select(CountTriangles).ToArray();

        Console.WriteLine("LOD triangle counts: " + string.Join(", ", triCounts));

        // Each successive LOD must have strictly fewer triangles
        for (int i = 1; i < triCounts.Length; i++)
        {
            triCounts[i].ShouldBeLessThan(triCounts[i - 1],
                $"LOD-{i} ({triCounts[i]} tris) should have fewer triangles than LOD-{i - 1} ({triCounts[i - 1]} tris)");
        }
    }
}
