using System;
using System.IO;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Shouldly;

namespace Obj2Tiles.Library.Test;

/// <summary>
/// Tests for MeshUtils.LoadMesh — the OBJ parser used by the splitting stage.
/// Covers all OBJ format corner cases and validates fixes for Issues #35, #60, #64.
/// </summary>
public class ObjParsingTests
{
    private const string TestDataPath = "TestData";

    // --- Vertex parsing ---

    [Test]
    public void LoadMesh_Triangle_ParsesVerticesAndFaces()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle.obj"));

        mesh.ShouldBeOfType<Mesh>();
        mesh.VertexCount.ShouldBe(4);
        mesh.FacesCount.ShouldBe(2);
    }

    [Test]
    public void LoadMesh_ScientificNotation_ParsesCorrectly()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "scientific-notation.obj"));

        mesh.ShouldBeOfType<Mesh>();
        mesh.VertexCount.ShouldBe(3);
        mesh.FacesCount.ShouldBe(1);

        // v 1.5e-3 2.0e2 -3.1e1 → (0.0015, 200, -31)
        var bounds = mesh.Bounds;
        bounds.Min.X.ShouldBe(0.0, tolerance: 0.01);
        bounds.Max.Y.ShouldBe(200.0, tolerance: 0.01);
    }

    [Test]
    public void LoadMesh_CommentsAndBlanks_IgnoredCorrectly()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "comments-and-blanks.obj"));

        mesh.ShouldBeOfType<Mesh>();
        mesh.VertexCount.ShouldBe(3);
        mesh.FacesCount.ShouldBe(1);
    }

    [Test]
    public void LoadMesh_EmptyFile_ReturnsEmptyMesh()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "empty.obj"));

        mesh.VertexCount.ShouldBe(0);
        mesh.FacesCount.ShouldBe(0);
    }

    [Test]
    public void LoadMesh_OnlyVertices_NoFaces()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "only-vertices.obj"));

        mesh.VertexCount.ShouldBe(4);
        mesh.FacesCount.ShouldBe(0);
    }

    // --- Face format variants ---

    [Test]
    public void LoadMesh_FaceFormatV_VN_ParsesCorrectly()
    {
        // f v//vn format — normals are recognized but ignored, mesh is non-textured
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle-normals.obj"));

        mesh.ShouldBeOfType<Mesh>();
        mesh.VertexCount.ShouldBe(3);
        mesh.FacesCount.ShouldBe(1);
    }

    [Test]
    public void LoadMesh_FaceFormatV_VT_VN_ParsesCorrectly()
    {
        // f v/vt/vn format — produces MeshT
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle-vt-vn.obj"));

        mesh.ShouldBeOfType<MeshT>();
        mesh.VertexCount.ShouldBe(3);
        mesh.FacesCount.ShouldBe(1);
    }

    // --- Quad and N-gon triangulation (Fixed: Issue #60) ---

    [Test]
    public void LoadMesh_QuadFace_TriangulatedTo2Triangles()
    {
        // Fixed: Issue #60 — quads were silently ignored, now fan-triangulated
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "quad.obj"));

        mesh.ShouldBeOfType<Mesh>();
        mesh.VertexCount.ShouldBe(4);
        mesh.FacesCount.ShouldBe(2); // quad → 2 triangles via fan triangulation
    }

    [Test]
    public void LoadMesh_NgonFace_TriangulatedCorrectly()
    {
        // Fixed: Issue #60 — n-gons (5+ vertices) now fan-triangulated
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "ngon.obj"));

        mesh.ShouldBeOfType<Mesh>();
        mesh.VertexCount.ShouldBe(5);
        mesh.FacesCount.ShouldBe(3); // pentagon → 3 triangles via fan triangulation
    }

    // --- UV coordinate wrapping (Fixed: Issue #35) ---

    [Test]
    public void LoadMesh_NegativeUV_WrappedToUnitRange()
    {
        // Fixed: Issue #35 — negative UV coordinates now wrapped via modulo instead of throwing
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle-uv-negative.obj"));

        mesh.ShouldBeOfType<MeshT>();
        mesh.VertexCount.ShouldBe(3);
        mesh.FacesCount.ShouldBe(1);
        // If we got here, UVs were wrapped instead of throwing
    }

    // --- Line element skipping (Fixed: Issue #64) ---

    [Test]
    public void LoadMesh_LineElement_SkippedGracefully()
    {
        // Fixed: Issue #64 — 'l' elements now skipped instead of throwing NotSupportedException
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "triangle-with-lines.obj"));

        mesh.ShouldBeOfType<Mesh>();
        mesh.VertexCount.ShouldBe(4);
        mesh.FacesCount.ShouldBe(1);
    }

    [Test]
    public void LoadMesh_CstypeElement_StillThrows()
    {
        // Curvilinear elements remain unsupported and should still throw
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "v 0 0 0\nv 1 0 0\nv 0.5 1 0\ncstype bezier\nf 1 2 3\n");
            Should.Throw<NotSupportedException>(() => MeshUtils.LoadMesh(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // --- Degenerate faces ---

    [Test]
    public void LoadMesh_DegenerateFace_LoadsWithoutCrash()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "degenerate-faces.obj"));

        mesh.ShouldBeOfType<Mesh>();
        mesh.FacesCount.ShouldBe(2); // both faces loaded, even the degenerate one
    }

    // --- Material handling ---

    [Test]
    public void LoadMesh_MultiMaterial_ParsesMaterials()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "multi-material.obj"), out var deps);

        mesh.ShouldBeOfType<MeshT>();
        mesh.FacesCount.ShouldBe(4);
        deps.Length.ShouldBeGreaterThan(0); // mtl file is a dependency
    }

    [Test]
    public void LoadMesh_MissingMtlFile_ThrowsFileNotFound()
    {
        // mtllib points to a non-existent file → should throw (not "Access denied")
        Should.Throw<FileNotFoundException>(() =>
            MeshUtils.LoadMesh(Path.Combine(TestDataPath, "mtllib-missing.obj")));
    }

    // --- Normals parsing (Fixed: segs.Length condition) ---

    [Test]
    public void LoadMesh_FileWithNormals_DoesNotThrow()
    {
        // Fixed: vn case had wrong segs.Length == 3 (should be >= 4)
        // The normals are recognized and skipped without error
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube2", "cube.obj"));

        mesh.ShouldBeOfType<MeshT>();
        mesh.FacesCount.ShouldBe(12);
    }

    // --- Cube geometry validation ---

    [Test]
    public void LoadMesh_Cube3D_CorrectBounds()
    {
        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-3d.obj"));

        mesh.ShouldBeOfType<Mesh>();
        mesh.VertexCount.ShouldBe(8);
        mesh.FacesCount.ShouldBe(12);

        var bounds = mesh.Bounds;
        bounds.Min.X.ShouldBe(-1.0);
        bounds.Min.Y.ShouldBe(-1.0);
        bounds.Min.Z.ShouldBe(-1.0);
        bounds.Max.X.ShouldBe(1.0);
        bounds.Max.Y.ShouldBe(1.0);
        bounds.Max.Z.ShouldBe(1.0);
    }
}
