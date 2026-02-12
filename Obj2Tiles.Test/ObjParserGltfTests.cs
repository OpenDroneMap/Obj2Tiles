using System.IO;
using System.Linq;
using NUnit.Framework;
using SilentWave.Obj2Gltf.WaveFront;
using Shouldly;

namespace Obj2Tiles.Test;

/// <summary>
/// Tests for ObjParser.Parse — the OBJ parser used by the glTF conversion stage.
/// Covers quad/ngon triangulation, groups, degenerate faces, and vertex colors.
/// </summary>
public class ObjParserGltfTests
{
    private const string TestDataPath = "TestData";
    private const string LibTestDataPath = TestDataPath;

    private ObjModel ParseFile(string path)
    {
        var parser = new ObjParser();
        return parser.Parse(path);
    }

    // --- Basic parsing ---

    [Test]
    public void Parse_Triangle_CorrectCounts()
    {
        var model = ParseFile(Path.Combine(LibTestDataPath, "triangle.obj"));

        model.Vertices.Count.ShouldBe(4);
        var totalTriangles = model.Geometries
            .SelectMany(g => g.Faces)
            .SelectMany(f => f.Triangles)
            .Count();
        totalTriangles.ShouldBe(2);
    }

    // --- Quad triangulation ---

    [Test]
    public void Parse_QuadFace_TriangulatedTo2Triangles()
    {
        var model = ParseFile(Path.Combine(LibTestDataPath, "quad-simple.obj"));

        model.Vertices.Count.ShouldBe(4);
        var totalTriangles = model.Geometries
            .SelectMany(g => g.Faces)
            .SelectMany(f => f.Triangles)
            .Count();
        totalTriangles.ShouldBe(2);
    }

    // --- N-gon triangulation ---

    [Test]
    public void Parse_NgonFace_Triangulated()
    {
        var model = ParseFile(Path.Combine(LibTestDataPath, "ngon.obj"));

        model.Vertices.Count.ShouldBe(5);
        var totalTriangles = model.Geometries
            .SelectMany(g => g.Faces)
            .SelectMany(f => f.Triangles)
            .Count();
        totalTriangles.ShouldBe(3); // pentagon → 3 triangles
    }

    // --- Vertex colors ---

    [Test]
    public void Parse_VertexColors_Parsed()
    {
        var model = ParseFile(Path.Combine(LibTestDataPath, "cube-colors-3d.obj"));

        model.Colors.Count.ShouldBe(8);
        model.Vertices.Count.ShouldBe(8);
    }

    [Test]
    public void Parse_NoColors_EmptyColorsList()
    {
        var model = ParseFile(Path.Combine(LibTestDataPath, "triangle.obj"));

        model.Colors.Count.ShouldBe(0);
    }

    // --- Degenerate faces ---

    [Test]
    public void Parse_DegenerateFaces_WithRemoval()
    {
        var parser = new ObjParser();
        var model = parser.Parse(Path.Combine(LibTestDataPath, "degenerate-faces.obj"), removeDegenerateFaces: true);

        // Should have removed the degenerate face (all 3 vertices coincident)
        var totalTriangles = model.Geometries
            .SelectMany(g => g.Faces)
            .SelectMany(f => f.Triangles)
            .Count();
        totalTriangles.ShouldBeLessThan(2); // at most 1 (the degenerate one removed)
    }

    [Test]
    public void Parse_DegenerateFaces_WithoutRemoval()
    {
        var parser = new ObjParser();
        var model = parser.Parse(Path.Combine(LibTestDataPath, "degenerate-faces.obj"), removeDegenerateFaces: false);

        var totalTriangles = model.Geometries
            .SelectMany(g => g.Faces)
            .SelectMany(f => f.Triangles)
            .Count();
        totalTriangles.ShouldBe(2); // both faces kept
    }

    // --- Empty file ---

    [Test]
    public void Parse_EmptyFile_ReturnsEmptyModel()
    {
        var model = ParseFile(Path.Combine(LibTestDataPath, "empty.obj"));

        model.Vertices.Count.ShouldBe(0);
        // ObjParser always creates a default geometry group, so Count is 1
        // but the geometry should have zero faces
        model.Geometries.Count().ShouldBe(1);
        model.Geometries.First().Faces.Count.ShouldBe(0);
    }

    // --- Scientific notation ---

    [Test]
    public void Parse_ScientificNotation_ParsesCorrectly()
    {
        var model = ParseFile(Path.Combine(LibTestDataPath, "scientific-notation.obj"));

        model.Vertices.Count.ShouldBe(3);
        // v 1.5e-3 → 0.0015
        model.Vertices[0].X.ShouldBe(0.0015f, tolerance: 0.0001f);
    }

    // --- Comments and blanks ---

    [Test]
    public void Parse_CommentsAndBlanks_ParsesCorrectly()
    {
        var model = ParseFile(Path.Combine(LibTestDataPath, "comments-and-blanks.obj"));

        model.Vertices.Count.ShouldBe(3);
    }

    // --- Normals ---

    [Test]
    public void Parse_FileWithNormals_NormalsAreParsed()
    {
        var model = ParseFile(Path.Combine(LibTestDataPath, "triangle-normals.obj"));

        model.Normals.Count.ShouldBe(1); // one normal shared by 3 face vertices
    }
}
