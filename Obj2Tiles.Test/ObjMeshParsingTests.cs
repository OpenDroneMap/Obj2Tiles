using System.IO;
using System.Linq;
using NUnit.Framework;
using Obj2Tiles.Stages.Model;
using Shouldly;

namespace Obj2Tiles.Test;

/// <summary>
/// Tests for ObjMesh.ReadFile — the OBJ parser used by the decimation stage.
/// Covers quad triangulation, negative indices, normals, and edge cases.
/// </summary>
public class ObjMeshParsingTests
{
    private const string TestDataPath = "TestData";

    private ObjMesh LoadMesh(string relativePath)
    {
        var mesh = new ObjMesh();
        mesh.ReadFile(Path.Combine(TestDataPath, relativePath));
        return mesh;
    }

    // --- Quad triangulation ---

    [Test]
    public void ReadFile_QuadFace_TriangulatedTo2Triangles()
    {
        var mesh = LoadMesh("quad-mesh.obj");

        mesh.Vertices.Length.ShouldBe(4);
        // Quad → 2 triangles = 6 indices in one sub-mesh
        var totalIndices = mesh.SubMeshIndices.Sum(s => s.Length);
        (totalIndices / 3).ShouldBe(2);
    }

    // --- Negative indices ---

    [Test]
    public void ReadFile_NegativeIndices_ResolvedCorrectly()
    {
        var mesh = LoadMesh("negative-idx.obj");

        mesh.Vertices.Length.ShouldBeGreaterThan(0);
        var totalTriangles = mesh.SubMeshIndices.Sum(s => s.Length) / 3;
        totalTriangles.ShouldBe(2);

        // All indices should be valid (non-negative, within bounds)
        foreach (var submesh in mesh.SubMeshIndices)
        {
            foreach (var idx in submesh)
            {
                idx.ShouldBeGreaterThanOrEqualTo(0);
                idx.ShouldBeLessThan(mesh.Vertices.Length);
            }
        }
    }

    // --- Normals ---

    [Test]
    public void ReadFile_FileWithNormals_NormalsAreParsed()
    {
        // ObjMesh fully parses normals (unlike MeshUtils.LoadMesh which skips them)
        var mesh = LoadMesh("cube2-normals.obj");

        mesh.Normals.ShouldNotBeNull();
        mesh.Normals!.Length.ShouldBeGreaterThan(0);
    }

    // --- Empty file ---

    [Test]
    public void ReadFile_EmptyFile_NoVerticesOrFaces()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "");
            var mesh = new ObjMesh();
            mesh.ReadFile(tempFile);

            mesh.Vertices.Length.ShouldBe(0);
            mesh.SubMeshIndices.Length.ShouldBe(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // --- Scientific notation ---

    [Test]
    public void ReadFile_ScientificNotation_ParsesCorrectly()
    {
        var mesh = LoadMesh("scientific-notation.obj");

        mesh.Vertices.Length.ShouldBeGreaterThan(0);
        var totalTriangles = mesh.SubMeshIndices.Sum(s => s.Length) / 3;
        totalTriangles.ShouldBe(1);
    }

    // --- Vertex colors ---

    [Test]
    public void ReadFile_VertexColors_Parsed()
    {
        var mesh = LoadMesh("cube-colors-3d.obj");

        mesh.VertexColors.ShouldNotBeNull();
        mesh.VertexColors!.Length.ShouldBe(mesh.Vertices.Length);
    }

    [Test]
    public void ReadFile_NoColors_VertexColorsIsNull()
    {
        var mesh = LoadMesh("cube-3d.obj");

        mesh.VertexColors.ShouldBeNull();
    }

    // --- Write/Read round-trip ---

    [Test]
    public void WriteRead_RoundTrip_PreservesGeometry()
    {
        var mesh = LoadMesh("quad-mesh.obj");
        var originalVertexCount = mesh.Vertices.Length;
        var originalTriangles = mesh.SubMeshIndices.Sum(s => s.Length) / 3;

        var tempFile = Path.GetTempFileName();
        try
        {
            mesh.WriteFile(tempFile);

            var reloaded = new ObjMesh();
            reloaded.ReadFile(tempFile);

            reloaded.Vertices.Length.ShouldBe(originalVertexCount);
            var reloadedTriangles = reloaded.SubMeshIndices.Sum(s => s.Length) / 3;
            reloadedTriangles.ShouldBe(originalTriangles);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // --- BUG: Issue #54 — usemtl without subsequent faces ---

    [Test]
    public void ReadFile_UsemtlWithoutFaces_DoesNotThrow()
    {
        // BUG: Issue #54 — usemtl at the end of file without subsequent faces
        // may cause InvalidOperationException in downstream processing.
        // This test checks basic parsing resilience.
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "v 0 0 0\nv 1 0 0\nv 0.5 1 0\nf 1 2 3\nusemtl OrphanMaterial\n");
            var mesh = new ObjMesh();
            // ReadFile should not crash even with trailing usemtl
            Should.NotThrow(() => mesh.ReadFile(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
