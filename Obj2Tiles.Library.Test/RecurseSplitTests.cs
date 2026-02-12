using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Shouldly;

namespace Obj2Tiles.Library.Test;

/// <summary>
/// Tests for RecurseSplitXY and RecurseSplitXYZ — the recursive mesh splitting algorithms.
/// These were previously untested.
/// </summary>
public class RecurseSplitTests
{
    private const string TestDataPath = "TestData";

    private IMesh LoadCube3D() => MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-3d.obj"));
    private IMesh LoadCubeColors3D() => MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-colors-3d.obj"));

    private static Vertex3 GetBaricenter(IMesh mesh) => mesh.GetVertexBaricenter();

    // --- RecurseSplitXY with bounds ---

    [Test]
    public async Task RecurseSplitXY_Depth0_ReturnsSameMesh()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXY(mesh, 0, mesh.Bounds, bag);

        bag.Count.ShouldBe(1);
        bag.First().FacesCount.ShouldBe(mesh.FacesCount);
    }

    [Test]
    public async Task RecurseSplitXY_Depth1_ProducesUpTo4Meshes()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXY(mesh, 1, mesh.Bounds, bag);

        bag.Count.ShouldBeGreaterThan(0);
        bag.Count.ShouldBeLessThanOrEqualTo(4);

        // All meshes should have faces
        foreach (var m in bag)
            m.FacesCount.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task RecurseSplitXY_Depth2_ProducesUpTo16Meshes()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXY(mesh, 2, mesh.Bounds, bag);

        bag.Count.ShouldBeGreaterThan(0);
        bag.Count.ShouldBeLessThanOrEqualTo(16);
    }

    [Test]
    public async Task RecurseSplitXY_AllMeshesHaveFaces()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXY(mesh, 2, mesh.Bounds, bag);

        foreach (var m in bag)
            m.FacesCount.ShouldBeGreaterThan(0, $"Mesh '{m.Name}' has no faces");
    }

    [Test]
    public async Task RecurseSplitXY_TotalFacesAtLeastOriginal()
    {
        var mesh = LoadCube3D();
        var originalFaces = mesh.FacesCount;
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXY(mesh, 1, mesh.Bounds, bag);

        var totalFaces = bag.Sum(m => m.FacesCount);
        totalFaces.ShouldBeGreaterThanOrEqualTo(originalFaces);
    }

    // --- RecurseSplitXY with getSplitPoint function ---

    [Test]
    public async Task RecurseSplitXY_WithFunc_ProducesUpTo4Meshes()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXY(mesh, 1, GetBaricenter, bag);

        bag.Count.ShouldBeGreaterThan(0);
        bag.Count.ShouldBeLessThanOrEqualTo(4);
    }

    // --- RecurseSplitXYZ ---

    [Test]
    public async Task RecurseSplitXYZ_Depth1_ProducesUpTo8Meshes()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYZ(mesh, 1, GetBaricenter, bag);

        bag.Count.ShouldBeGreaterThan(0);
        bag.Count.ShouldBeLessThanOrEqualTo(8);

        foreach (var m in bag)
            m.FacesCount.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task RecurseSplitXYZ_WithBounds_Depth1_ProducesUpTo8Meshes()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYZ(mesh, 1, mesh.Bounds, bag);

        bag.Count.ShouldBeGreaterThan(0);
        bag.Count.ShouldBeLessThanOrEqualTo(8);
    }

    // --- Vertex colors through recursion ---

    [Test]
    public async Task RecurseSplitXY_WithColors_ColorsPreserved()
    {
        var mesh = LoadCubeColors3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXY(mesh, 1, mesh.Bounds, bag);

        bag.Count.ShouldBeGreaterThan(0);

        foreach (var m in bag)
        {
            var typedMesh = m.ShouldBeOfType<Mesh>();
            typedMesh.VertexColors.ShouldNotBeNull();
            typedMesh.VertexColors!.Count.ShouldBe(m.VertexCount);
        }
    }

    [Test]
    public async Task RecurseSplitXYZ_WithColors_ColorsPreserved()
    {
        var mesh = LoadCubeColors3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYZ(mesh, 1, mesh.Bounds, bag);

        bag.Count.ShouldBeGreaterThan(0);

        foreach (var m in bag)
        {
            var typedMesh = m.ShouldBeOfType<Mesh>();
            typedMesh.VertexColors.ShouldNotBeNull();
            typedMesh.VertexColors!.Count.ShouldBe(m.VertexCount);
        }
    }

    // --- Union of bounds covers original ---

    [Test]
    public async Task RecurseSplitXY_UnionBoundsCoversOriginal()
    {
        var mesh = LoadCube3D();
        var originalBounds = mesh.Bounds;
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXY(mesh, 1, mesh.Bounds, bag);

        var allMeshes = bag.ToList();
        var minX = allMeshes.Min(m => m.Bounds.Min.X);
        var minY = allMeshes.Min(m => m.Bounds.Min.Y);
        var maxX = allMeshes.Max(m => m.Bounds.Max.X);
        var maxY = allMeshes.Max(m => m.Bounds.Max.Y);

        minX.ShouldBeLessThanOrEqualTo(originalBounds.Min.X + 1e-9);
        minY.ShouldBeLessThanOrEqualTo(originalBounds.Min.Y + 1e-9);
        maxX.ShouldBeGreaterThanOrEqualTo(originalBounds.Max.X - 1e-9);
        maxY.ShouldBeGreaterThanOrEqualTo(originalBounds.Max.Y - 1e-9);
    }
}
