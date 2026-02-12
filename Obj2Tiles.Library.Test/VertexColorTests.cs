using System.IO;
using System.Linq;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Library.Materials;
using Shouldly;
using Path = System.IO.Path;

namespace Obj2Tiles.Library.Test;

/// <summary>
/// Tests for vertex color support across the Library layer:
/// RGB type extensions, MeshUtils parsing, Mesh/MeshT split, WriteObj round-trip.
/// </summary>
public class VertexColorTests
{
    private const string TestDataPath = "TestData";
    private const string TestOutputPath = "TestOutput";

    private static readonly IVertexUtils xutils = new VertexUtilsX();

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

    #region RGB Tests

    [Test]
    public void RGB_CutEdgePerc_Midpoint()
    {
        var a = new RGB(0, 0, 0);
        var b = new RGB(1, 1, 1);
        var mid = a.CutEdgePerc(b, 0.5);
        mid.R.ShouldBe(0.5, 1e-10);
        mid.G.ShouldBe(0.5, 1e-10);
        mid.B.ShouldBe(0.5, 1e-10);
    }

    [Test]
    public void RGB_CutEdgePerc_ZeroReturnsA()
    {
        var a = new RGB(0.2, 0.3, 0.4);
        var b = new RGB(0.8, 0.9, 1.0);
        var result = a.CutEdgePerc(b, 0.0);
        result.R.ShouldBe(0.2, 1e-10);
        result.G.ShouldBe(0.3, 1e-10);
        result.B.ShouldBe(0.4, 1e-10);
    }

    [Test]
    public void RGB_CutEdgePerc_OneReturnsB()
    {
        var a = new RGB(0.2, 0.3, 0.4);
        var b = new RGB(0.8, 0.9, 1.0);
        var result = a.CutEdgePerc(b, 1.0);
        result.R.ShouldBe(0.8, 1e-10);
        result.G.ShouldBe(0.9, 1e-10);
        result.B.ShouldBe(1.0, 1e-10);
    }

    [Test]
    public void RGB_Equality()
    {
        var a = new RGB(0.5, 0.5, 0.5);
        var b = new RGB(0.5, 0.5, 0.5);
        a.Equals(b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Test]
    public void RGB_Inequality()
    {
        var a = new RGB(0.5, 0.5, 0.5);
        var b = new RGB(0.5, 0.6, 0.5);
        a.Equals(b).ShouldBeFalse();
    }

    [Test]
    public void RGB_SrgbToLinear_Zero()
    {
        var result = RGB.SrgbToLinear(new RGB(0, 0, 0));
        result.R.ShouldBe(0, 1e-10);
        result.G.ShouldBe(0, 1e-10);
        result.B.ShouldBe(0, 1e-10);
    }

    [Test]
    public void RGB_SrgbToLinear_One()
    {
        var result = RGB.SrgbToLinear(new RGB(1, 1, 1));
        result.R.ShouldBe(1.0, 1e-6);
        result.G.ShouldBe(1.0, 1e-6);
        result.B.ShouldBe(1.0, 1e-6);
    }

    [Test]
    public void RGB_SrgbToLinear_BelowThreshold()
    {
        // Values <= 0.04045 use linear formula: c / 12.92
        var color = new RGB(0.04, 0.02, 0.01);
        var result = RGB.SrgbToLinear(color);
        result.R.ShouldBe(0.04 / 12.92, 1e-10);
        result.G.ShouldBe(0.02 / 12.92, 1e-10);
        result.B.ShouldBe(0.01 / 12.92, 1e-10);
    }

    [Test]
    public void RGB_SrgbToLinear_AboveThreshold()
    {
        // sRGB 0.5 → linear ~0.2140
        var color = new RGB(0.5, 0.5, 0.5);
        var result = RGB.SrgbToLinear(color);
        result.R.ShouldBe(System.Math.Pow((0.5 + 0.055) / 1.055, 2.4), 1e-6);
    }

    #endregion

    #region MeshUtils Parsing Tests

    [Test]
    public void LoadMesh_TriangleColors_ParsesVertexColors()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle-colors.obj"));

        mesh.ShouldBeOfType<Mesh>();
        var m = (Mesh)mesh;
        m.VertexColors.ShouldNotBeNull();
        m.VertexColors!.Count.ShouldBe(4);
        // First vertex: red (1, 0, 0)
        m.VertexColors[0].R.ShouldBe(1.0, 1e-6);
        m.VertexColors[0].G.ShouldBe(0.0, 1e-6);
        m.VertexColors[0].B.ShouldBe(0.0, 1e-6);
        // Second vertex: green (0, 1, 0)
        m.VertexColors[1].G.ShouldBe(1.0, 1e-6);
        // Third vertex: blue (0, 0, 1)
        m.VertexColors[2].B.ShouldBe(1.0, 1e-6);
        // Fourth vertex: yellow (1, 1, 0)
        m.VertexColors[3].R.ShouldBe(1.0, 1e-6);
        m.VertexColors[3].G.ShouldBe(1.0, 1e-6);
        m.VertexColors[3].B.ShouldBe(0.0, 1e-6);
    }

    [Test]
    public void LoadMesh_CubeColors_ParsesVertexColors()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-colors.obj"));

        mesh.ShouldBeOfType<Mesh>();
        var m = (Mesh)mesh;
        m.VertexColors.ShouldNotBeNull();
        m.VertexColors!.Count.ShouldBe(8);
    }

    [Test]
    public void LoadMesh_CubeColorsTextured_ParsesVertexColors()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-colors-textured/cube-colors-textured.obj"));

        mesh.ShouldBeOfType<MeshT>();
        var m = (MeshT)mesh;
        m.VertexColors.ShouldNotBeNull();
        m.VertexColors!.Count.ShouldBe(8);
    }

    [Test]
    public void LoadMesh_Triangle_NoColors()
    {
        // Standard triangle without colors should have null VertexColors
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle.obj"));

        mesh.ShouldBeOfType<Mesh>();
        var m = (Mesh)mesh;
        m.VertexColors.ShouldBeNull();
    }

    #endregion

    #region Split Tests

    [Test]
    public void Split_TriangleColors_PreservesColors()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle-colors.obj"));
        var center = mesh.GetVertexBaricenter();

        mesh.Split(xutils, center.X, out var left, out var right);

        var leftMesh = (Mesh)left;
        var rightMesh = (Mesh)right;

        leftMesh.VertexColors.ShouldNotBeNull();
        rightMesh.VertexColors.ShouldNotBeNull();

        // Total vertex count may increase due to intersection vertices, but should be >= original
        (leftMesh.VertexColors!.Count + rightMesh.VertexColors!.Count).ShouldBeGreaterThanOrEqualTo(4);
    }

    [Test]
    public void Split_CubeColors_PreservesColors()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-colors.obj"));
        var center = mesh.GetVertexBaricenter();

        mesh.Split(xutils, center.X, out var left, out var right);

        var leftMesh = (Mesh)left;
        var rightMesh = (Mesh)right;

        leftMesh.VertexColors.ShouldNotBeNull();
        rightMesh.VertexColors.ShouldNotBeNull();

        // Each half should have colors for every vertex
        leftMesh.VertexColors!.Count.ShouldBeGreaterThan(0);
        rightMesh.VertexColors!.Count.ShouldBeGreaterThan(0);
    }

    [Test]
    public void Split_CubeColorsTextured_PreservesColors()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-colors-textured/cube-colors-textured.obj"));
        var center = mesh.GetVertexBaricenter();

        mesh.Split(xutils, center.X, out var left, out var right);

        var leftMesh = (MeshT)left;
        var rightMesh = (MeshT)right;

        leftMesh.VertexColors.ShouldNotBeNull();
        rightMesh.VertexColors.ShouldNotBeNull();
    }

    [Test]
    public void Split_ColorInterpolation_AtMidpoint()
    {
        // Create a triangle that straddles x=0.5 exactly.
        // V1 (0,0,0) red, V2 (1,0,0) green, V3 (0.5,1,0) yellow
        // Splitting at x=0.5 should create intersection vertices with interpolated colors.
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle-colors.obj"));

        // Split at x=0.5 (the barycenter X of this triangle)
        mesh.Split(xutils, 0.5, out var left, out var right);

        var leftMesh = (Mesh)left;
        var rightMesh = (Mesh)right;

        // Both sides should have vertex colors
        leftMesh.VertexColors.ShouldNotBeNull();
        rightMesh.VertexColors.ShouldNotBeNull();

        // Check that interpolated colors are reasonable (between 0 and 1)
        foreach (var c in leftMesh.VertexColors!)
        {
            c.R.ShouldBeInRange(0.0, 1.0);
            c.G.ShouldBeInRange(0.0, 1.0);
            c.B.ShouldBeInRange(0.0, 1.0);
        }
        foreach (var c in rightMesh.VertexColors!)
        {
            c.R.ShouldBeInRange(0.0, 1.0);
            c.G.ShouldBeInRange(0.0, 1.0);
            c.B.ShouldBeInRange(0.0, 1.0);
        }
    }

    #endregion

    #region WriteObj Round-Trip Tests

    [Test]
    public void WriteObj_TriangleColors_RoundTrip()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_TriangleColors_RoundTrip));
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle-colors.obj"));

        var outFile = Path.Combine(testPath, "triangle-colors-out.obj");
        mesh.WriteObj(outFile);

        // Re-load and verify colors are preserved
        var reloaded = MeshUtils.LoadMesh(outFile);
        reloaded.ShouldBeOfType<Mesh>();
        var rm = (Mesh)reloaded;
        rm.VertexColors.ShouldNotBeNull();
        rm.VertexColors!.Count.ShouldBe(4);
        rm.VertexColors[0].R.ShouldBe(1.0, 1e-4);
        rm.VertexColors[0].G.ShouldBe(0.0, 1e-4);
    }

    [Test]
    public void WriteObj_CubeColors_RoundTrip()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_CubeColors_RoundTrip));
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-colors.obj"));

        var outFile = Path.Combine(testPath, "cube-colors-out.obj");
        mesh.WriteObj(outFile);

        var reloaded = MeshUtils.LoadMesh(outFile);
        reloaded.ShouldBeOfType<Mesh>();
        var rm = (Mesh)reloaded;
        rm.VertexColors.ShouldNotBeNull();
        rm.VertexColors!.Count.ShouldBe(8);
    }

    [Test]
    public void WriteObj_SplitColors_RoundTrip()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_SplitColors_RoundTrip));
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle-colors.obj"));
        var center = mesh.GetVertexBaricenter();

        mesh.Split(xutils, center.X, out var left, out var right);

        var leftFile = Path.Combine(testPath, "left.obj");
        var rightFile = Path.Combine(testPath, "right.obj");
        left.WriteObj(leftFile);
        right.WriteObj(rightFile);

        // Re-load both halves
        var leftReloaded = MeshUtils.LoadMesh(leftFile);
        var rightReloaded = MeshUtils.LoadMesh(rightFile);

        ((Mesh)leftReloaded).VertexColors.ShouldNotBeNull();
        ((Mesh)rightReloaded).VertexColors.ShouldNotBeNull();
    }

    [Test]
    public void WriteObj_NoColors_StillWorks()
    {
        // Ensure no regression: meshes without colors still work
        var testPath = GetTestOutputPath(nameof(WriteObj_NoColors_StillWorks));
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle.obj"));

        var outFile = Path.Combine(testPath, "triangle-out.obj");
        mesh.WriteObj(outFile);

        var reloaded = MeshUtils.LoadMesh(outFile);
        ((Mesh)reloaded).VertexColors.ShouldBeNull();
    }

    [Test]
    public void WriteObj_CubeNoColors_StillWorks()
    {
        // Ensure no regression: textured cube without colors still works
        var testPath = GetTestOutputPath(nameof(WriteObj_CubeNoColors_StillWorks));
        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube/cube.obj"));
        mesh.TexturesStrategy = TexturesStrategy.KeepOriginal;

        var outFile = Path.Combine(testPath, "cube-out.obj");
        mesh.WriteObj(outFile);

        var reloaded = (MeshT)MeshUtils.LoadMesh(outFile);
        reloaded.VertexColors.ShouldBeNull();
    }

    #endregion
}
