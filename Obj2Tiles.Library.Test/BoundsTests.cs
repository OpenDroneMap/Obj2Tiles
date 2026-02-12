using System.IO;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Shouldly;

namespace Obj2Tiles.Library.Test;

/// <summary>
/// Tests for Box3 bounding box and mesh Bounds calculation.
/// </summary>
public class BoundsTests
{
    private const string TestDataPath = "TestData";

    [Test]
    public void Bounds_Triangle_Correct()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle.obj"));
        var bounds = mesh.Bounds;

        bounds.Min.X.ShouldBe(0.0);
        bounds.Min.Y.ShouldBe(0.0);
        bounds.Min.Z.ShouldBe(0.0);
        bounds.Max.X.ShouldBe(1.0);
        bounds.Max.Y.ShouldBe(1.0);
        bounds.Max.Z.ShouldBe(0.0);
    }

    [Test]
    public void Bounds_Cube3D_SymmetricAroundOrigin()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-3d.obj"));
        var bounds = mesh.Bounds;

        bounds.Min.X.ShouldBe(-1.0);
        bounds.Min.Y.ShouldBe(-1.0);
        bounds.Min.Z.ShouldBe(-1.0);
        bounds.Max.X.ShouldBe(1.0);
        bounds.Max.Y.ShouldBe(1.0);
        bounds.Max.Z.ShouldBe(1.0);

        bounds.Width.ShouldBe(2.0);
        bounds.Height.ShouldBe(2.0);
        bounds.Depth.ShouldBe(2.0);
    }

    [Test]
    public void Bounds_Center_IsCorrect()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-3d.obj"));
        var center = mesh.Bounds.Center;

        center.X.ShouldBe(0.0);
        center.Y.ShouldBe(0.0);
        center.Z.ShouldBe(0.0);
    }

    [Test]
    public void Bounds_AfterSplit_SubsetsOfOriginal()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-3d.obj"));
        var originalBounds = mesh.Bounds;
        var xutils = new VertexUtilsX();

        mesh.Split(xutils, 0.0, out var left, out var right);

        var lb = left.Bounds;
        lb.Min.X.ShouldBeGreaterThanOrEqualTo(originalBounds.Min.X - 1e-9);
        lb.Max.X.ShouldBeLessThanOrEqualTo(0.0 + 1e-9);

        var rb = right.Bounds;
        rb.Min.X.ShouldBeGreaterThanOrEqualTo(0.0 - 1e-9);
        rb.Max.X.ShouldBeLessThanOrEqualTo(originalBounds.Max.X + 1e-9);
    }

    // --- Box3 Split ---

    [Test]
    public void Box3_SplitX_CoversFull()
    {
        var box = new Box3(-1, -1, -1, 1, 1, 1);
        var halves = box.Split(Axis.X);

        halves.Length.ShouldBe(2);
        halves[0].Min.X.ShouldBe(-1.0);
        halves[0].Max.X.ShouldBe(0.0);
        halves[1].Min.X.ShouldBe(0.0);
        halves[1].Max.X.ShouldBe(1.0);

        // Y and Z unchanged
        halves[0].Min.Y.ShouldBe(-1.0);
        halves[0].Max.Y.ShouldBe(1.0);
    }

    [Test]
    public void Box3_SplitY_CoversFull()
    {
        var box = new Box3(-1, -1, -1, 1, 1, 1);
        var halves = box.Split(Axis.Y);

        halves[0].Max.Y.ShouldBe(0.0);
        halves[1].Min.Y.ShouldBe(0.0);
    }

    [Test]
    public void Box3_SplitZ_CoversFull()
    {
        var box = new Box3(-1, -1, -1, 1, 1, 1);
        var halves = box.Split(Axis.Z);

        halves[0].Max.Z.ShouldBe(0.0);
        halves[1].Min.Z.ShouldBe(0.0);
    }

    [Test]
    public void Box3_SplitAtPosition_Correct()
    {
        var box = new Box3(0, 0, 0, 10, 10, 10);
        var halves = box.Split(Axis.X, 3.0);

        halves[0].Max.X.ShouldBe(3.0);
        halves[1].Min.X.ShouldBe(3.0);
    }

    [Test]
    public void Box3_Center_Calculation()
    {
        var box = new Box3(2, 4, 6, 8, 10, 12);
        var center = box.Center;

        center.X.ShouldBe(5.0);
        center.Y.ShouldBe(7.0);
        center.Z.ShouldBe(9.0);
    }

    [Test]
    public void Box3_Equality()
    {
        var a = new Box3(0, 0, 0, 1, 1, 1);
        var b = new Box3(0, 0, 0, 1, 1, 1);
        var c = new Box3(0, 0, 0, 2, 1, 1);

        (a == b).ShouldBeTrue();
        (a != c).ShouldBeTrue();
    }
}
