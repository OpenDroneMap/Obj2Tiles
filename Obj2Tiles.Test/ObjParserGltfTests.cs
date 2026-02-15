using System;
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

    [Test]
    public void Parse_DefaultMaterial_HasName()
    {
        // The default material inserted by ObjParser must have Name="default"
        // so that GetMaterialIndexOrDefault can find it and avoid falling back
        // to the grey GetDefault() material.
        var model = ParseFile(Path.Combine(LibTestDataPath, "triangle.obj"));

        model.Materials.Count.ShouldBeGreaterThanOrEqualTo(1);
        model.Materials[0].Name.ShouldBe("default");
    }

    [Test]
    public void ConvertMaterial_DiffuseColorOnly_BaseColorFactorMatchesKd()
    {
        // Issue #36 — material with Kd but no map_Kd should produce
        // BaseColorFactor reflecting the Kd color, not grey (0.5).
        var mat = new SilentWave.Obj2Gltf.WaveFront.Material
        {
            Name = "ColorMat",
            Diffuse = new SilentWave.Obj2Gltf.WaveFront.Reflectivity(
                new SilentWave.Obj2Gltf.WaveFront.FactorColor(0.8, 0.2, 0.3))
        };

        var gltfMat = SilentWave.Obj2Gltf.Converter.ConvertMaterial(mat, _ => 0);

        gltfMat.PbrMetallicRoughness.ShouldNotBeNull();
        var bcf = gltfMat.PbrMetallicRoughness.BaseColorFactor;
        bcf.ShouldNotBeNull();
        bcf[0].ShouldBe(0.8, tolerance: 0.001);
        bcf[1].ShouldBe(0.2, tolerance: 0.001);
        bcf[2].ShouldBe(0.3, tolerance: 0.001);
        bcf[3].ShouldBe(1.0, tolerance: 0.001);
    }

    [Test]
    public void Parse_MaterialFromMtl_DiffuseColorPreservedEndToEnd()
    {
        // Issue #36 end-to-end: OBJ references MTL with only Kd (no map_Kd).
        // After ObjParser + MtlParser merge, the material must carry the
        // correct diffuse color so that ConvertMaterial does NOT produce grey.
        var tempDir = Path.Combine(Path.GetTempPath(), "obj2tiles_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var mtlPath = Path.Combine(tempDir, "color.mtl");
            var objPath = Path.Combine(tempDir, "color.obj");

            File.WriteAllText(mtlPath, "newmtl RedMat\nKd 0.9 0.1 0.2\n");
            File.WriteAllText(objPath,
                "mtllib color.mtl\nv 0 0 0\nv 1 0 0\nv 0.5 1 0\nusemtl RedMat\nf 1 2 3\n");

            // Simulate the same flow as Converter.Convert:
            // 1. ObjParser.Parse adds default material
            // 2. MtlParser adds materials from .mtl file
            var parser = new ObjParser();
            var model = parser.Parse(objPath);

            var mtlParser = new MtlParser();
            var mats = mtlParser.Parse(mtlPath);
            model.Materials.AddRange(mats);

            // RedMat must be found by name
            var redMat = model.Materials.FirstOrDefault(m => m.Name == "RedMat");
            redMat.ShouldNotBeNull();
            redMat!.Diffuse.ShouldNotBeNull();
            redMat.Diffuse.Color.Red.ShouldBe(0.9, tolerance: 0.001);

            // ConvertMaterial must produce the correct BaseColorFactor
            var gltfMat = SilentWave.Obj2Gltf.Converter.ConvertMaterial(
                redMat, _ => 0);
            gltfMat.PbrMetallicRoughness.BaseColorFactor[0].ShouldBe(0.9, tolerance: 0.001);
            gltfMat.PbrMetallicRoughness.BaseColorFactor[1].ShouldBe(0.1, tolerance: 0.001);
            gltfMat.PbrMetallicRoughness.BaseColorFactor[2].ShouldBe(0.2, tolerance: 0.001);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
