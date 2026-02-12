using System.IO;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Shouldly;

namespace Obj2Tiles.Library.Test;

/// <summary>
/// Tests for mesh splitting on all three axes (X, Y, Z).
/// Validates geometry correctness, vertex color preservation, and edge intersection handling.
/// The existing test suite only tested split on X axis — these tests cover Y and Z.
/// </summary>
public class SplitMultiAxisTests
{
    private const string TestDataPath = "TestData";

    private static readonly IVertexUtils XUtils = new VertexUtilsX();
    private static readonly IVertexUtils YUtils = new VertexUtilsY();
    private static readonly IVertexUtils ZUtils = new VertexUtilsZ();

    private IMesh LoadCube3D() => MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-3d.obj"));
    private IMesh LoadCubeColors3D() => MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-colors-3d.obj"));
    private IMesh LoadCubeTextured() => MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube2", "cube.obj"));

    // --- Split on all three axes ---

    [Test]
    public void SplitX_Cube_ProducesTwoHalves()
    {
        var mesh = LoadCube3D();
        var center = mesh.Bounds.Center;

        mesh.Split(XUtils, center.X, out var left, out var right);

        left.FacesCount.ShouldBeGreaterThan(0);
        right.FacesCount.ShouldBeGreaterThan(0);
    }

    [Test]
    public void SplitY_Cube_ProducesTwoHalves()
    {
        var mesh = LoadCube3D();
        var center = mesh.Bounds.Center;

        mesh.Split(YUtils, center.Y, out var left, out var right);

        left.FacesCount.ShouldBeGreaterThan(0);
        right.FacesCount.ShouldBeGreaterThan(0);
    }

    [Test]
    public void SplitZ_Cube_ProducesTwoHalves()
    {
        var mesh = LoadCube3D();
        var center = mesh.Bounds.Center;

        mesh.Split(ZUtils, center.Z, out var left, out var right);

        left.FacesCount.ShouldBeGreaterThan(0);
        right.FacesCount.ShouldBeGreaterThan(0);
    }

    // --- Geometry correctness: vertices are on the correct side ---

    [Test]
    public void SplitX_Cube_LeftVerticesHaveXLessOrEqualThreshold()
    {
        var mesh = LoadCube3D();
        var q = mesh.Bounds.Center.X;

        mesh.Split(XUtils, q, out var left, out _);

        var bounds = left.Bounds;
        bounds.Max.X.ShouldBeLessThanOrEqualTo(q + 1e-9);
    }

    [Test]
    public void SplitY_Cube_LeftVerticesHaveYLessOrEqualThreshold()
    {
        var mesh = LoadCube3D();
        var q = mesh.Bounds.Center.Y;

        mesh.Split(YUtils, q, out var left, out _);

        var bounds = left.Bounds;
        bounds.Max.Y.ShouldBeLessThanOrEqualTo(q + 1e-9);
    }

    [Test]
    public void SplitZ_Cube_LeftVerticesHaveZLessOrEqualThreshold()
    {
        var mesh = LoadCube3D();
        var q = mesh.Bounds.Center.Z;

        mesh.Split(ZUtils, q, out var left, out _);

        var bounds = left.Bounds;
        bounds.Max.Z.ShouldBeLessThanOrEqualTo(q + 1e-9);
    }

    // --- Face count invariant: split can only create more faces ---

    [Test]
    [TestCase("X")]
    [TestCase("Y")]
    [TestCase("Z")]
    public void Split_TotalFaceCount_GreaterOrEqualOriginal(string axis)
    {
        var mesh = LoadCube3D();
        var originalFaces = mesh.FacesCount;
        var center = mesh.Bounds.Center;

        var utils = axis switch { "X" => XUtils, "Y" => YUtils, _ => ZUtils };
        var q = axis switch { "X" => center.X, "Y" => center.Y, _ => center.Z };

        mesh.Split(utils, q, out var left, out var right);

        (left.FacesCount + right.FacesCount).ShouldBeGreaterThanOrEqualTo(originalFaces);
    }

    // --- Split at boundary: one side should be empty ---

    [Test]
    public void SplitX_AtMinBoundary_RightContainsAll()
    {
        var mesh = LoadCube3D();
        var bounds = mesh.Bounds;

        // Split below minimum — everything should be on the right
        mesh.Split(XUtils, bounds.Min.X - 1.0, out var left, out var right);

        left.FacesCount.ShouldBe(0);
        right.FacesCount.ShouldBe(mesh.FacesCount);
    }

    [Test]
    public void SplitX_AtMaxBoundary_LeftContainsAll()
    {
        var mesh = LoadCube3D();
        var bounds = mesh.Bounds;

        // Split above maximum — everything should be on the left
        mesh.Split(XUtils, bounds.Max.X + 1.0, out var left, out var right);

        left.FacesCount.ShouldBe(mesh.FacesCount);
        right.FacesCount.ShouldBe(0);
    }

    // --- Vertex colors through split on all axes ---

    [Test]
    [TestCase("X")]
    [TestCase("Y")]
    [TestCase("Z")]
    public void Split_WithColors_ColorsPreservedOnBothSides(string axis)
    {
        var mesh = LoadCubeColors3D();
        var center = mesh.Bounds.Center;

        var utils = axis switch { "X" => XUtils, "Y" => YUtils, _ => ZUtils };
        var q = axis switch { "X" => center.X, "Y" => center.Y, _ => center.Z };

        mesh.Split(utils, q, out var left, out var right);

        left.FacesCount.ShouldBeGreaterThan(0);
        right.FacesCount.ShouldBeGreaterThan(0);

        // Both halves should be Mesh with colors (not MeshT since no textures)
        var leftMesh = left.ShouldBeOfType<Mesh>();
        var rightMesh = right.ShouldBeOfType<Mesh>();

        leftMesh.VertexColors.ShouldNotBeNull();
        rightMesh.VertexColors.ShouldNotBeNull();
        leftMesh.VertexColors!.Count.ShouldBe(left.VertexCount);
        rightMesh.VertexColors!.Count.ShouldBe(right.VertexCount);
    }

    // --- Textured mesh split on Y and Z ---

    [Test]
    public void SplitY_MeshT_ProducesTwoTexturedHalves()
    {
        var mesh = LoadCubeTextured();
        var center = mesh.Bounds.Center;

        mesh.Split(YUtils, center.Y, out var left, out var right);

        left.FacesCount.ShouldBeGreaterThan(0);
        right.FacesCount.ShouldBeGreaterThan(0);
        left.ShouldBeOfType<MeshT>();
        right.ShouldBeOfType<MeshT>();
    }

    [Test]
    public void SplitZ_MeshT_ProducesTwoTexturedHalves()
    {
        var mesh = LoadCubeTextured();
        var center = mesh.Bounds.Center;

        mesh.Split(ZUtils, center.Z, out var left, out var right);

        left.FacesCount.ShouldBeGreaterThan(0);
        right.FacesCount.ShouldBeGreaterThan(0);
        left.ShouldBeOfType<MeshT>();
        right.ShouldBeOfType<MeshT>();
    }

    // --- Split bounds are subsets of original bounds ---

    [Test]
    [TestCase("X")]
    [TestCase("Y")]
    [TestCase("Z")]
    public void Split_BoundsAreSubsetsOfOriginal(string axis)
    {
        var mesh = LoadCube3D();
        var originalBounds = mesh.Bounds;
        var center = originalBounds.Center;

        var utils = axis switch { "X" => XUtils, "Y" => YUtils, _ => ZUtils };
        var q = axis switch { "X" => center.X, "Y" => center.Y, _ => center.Z };

        mesh.Split(utils, q, out var left, out var right);

        if (left.FacesCount > 0)
        {
            var lb = left.Bounds;
            lb.Min.X.ShouldBeGreaterThanOrEqualTo(originalBounds.Min.X - 1e-9);
            lb.Min.Y.ShouldBeGreaterThanOrEqualTo(originalBounds.Min.Y - 1e-9);
            lb.Min.Z.ShouldBeGreaterThanOrEqualTo(originalBounds.Min.Z - 1e-9);
        }

        if (right.FacesCount > 0)
        {
            var rb = right.Bounds;
            rb.Max.X.ShouldBeLessThanOrEqualTo(originalBounds.Max.X + 1e-9);
            rb.Max.Y.ShouldBeLessThanOrEqualTo(originalBounds.Max.Y + 1e-9);
            rb.Max.Z.ShouldBeLessThanOrEqualTo(originalBounds.Max.Z + 1e-9);
        }
    }
}
