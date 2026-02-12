using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Obj2Tiles.Stages.Model;
using Shouldly;

namespace Obj2Tiles.Test;

/// <summary>
/// Tests for Copilot review corner cases on ObjMesh vertex color handling:
/// 1. Mixed colored/non-colored vertices (index alignment)
/// 2. Malformed alpha channel (TryParse resilience)
/// 3. Alpha round-trip preservation through WriteFile/ReadFile
/// </summary>
public class ObjMeshVertexColorEdgeCaseTests
{
    private const string TestDataPath = "TestData";
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

    #region Fix 1: Mixed colored/non-colored vertices — index alignment

    [Test]
    public void ReadFile_MixedColors_IndicesAligned()
    {
        // mixed-colors.obj: v1 has color, v2 has color, v3 has NO color, v4 has color
        // Before fix: readColorList had 3 entries for 4 vertices → index mismatch
        // After fix: readColorList is padded with default white (1,1,1,1) for v3
        var mesh = new ObjMesh();
        mesh.ReadFile(Path.Combine(TestDataPath, "mixed-colors.obj"));

        mesh.VertexColors.ShouldNotBeNull();
        mesh.VertexColors.Length.ShouldBe(mesh.Vertices.Length);
    }

    [Test]
    public void ReadFile_MixedColors_DefaultColorIsWhite()
    {
        // The non-colored vertex (v3) should get default white (1,1,1,1)
        var mesh = new ObjMesh();
        mesh.ReadFile(Path.Combine(TestDataPath, "mixed-colors.obj"));

        // Find a white vertex — there should be at least one from the non-colored v3
        var hasWhite = mesh.VertexColors.Any(c =>
            Math.Abs(c.x - 1f) < 0.001f &&
            Math.Abs(c.y - 1f) < 0.001f &&
            Math.Abs(c.z - 1f) < 0.001f &&
            Math.Abs(c.w - 1f) < 0.001f);
        hasWhite.ShouldBeTrue("Non-colored vertices should get default white color");
    }

    [Test]
    public void ReadFile_LateColors_BackFillsPreviousVertices()
    {
        // late-colors.obj: v1 no color, v2 no color, v3 has color
        // readColorList is created at v3 and must back-fill defaults for v1, v2
        var mesh = new ObjMesh();
        mesh.ReadFile(Path.Combine(TestDataPath, "late-colors.obj"));

        mesh.VertexColors.ShouldNotBeNull();
        mesh.VertexColors.Length.ShouldBe(mesh.Vertices.Length);

        // v3 (last in original OBJ) should have the explicit color (0.5, 0.5, 0.5)
        var hasGray = mesh.VertexColors.Any(c =>
            Math.Abs(c.x - 0.5f) < 0.01f &&
            Math.Abs(c.y - 0.5f) < 0.01f &&
            Math.Abs(c.z - 0.5f) < 0.01f);
        hasGray.ShouldBeTrue("The explicitly colored vertex should retain its color");
    }

    [Test]
    public void ReadFile_MixedColors_WriteRoundTrip_PreservesAlignment()
    {
        // Read mixed-colors, write it, re-read — vertex colors must stay aligned
        var testPath = GetTestOutputPath(nameof(ReadFile_MixedColors_WriteRoundTrip_PreservesAlignment));
        var mesh = new ObjMesh();
        mesh.ReadFile(Path.Combine(TestDataPath, "mixed-colors.obj"));

        var outPath = Path.Combine(testPath, "mixed-out.obj");
        mesh.WriteFile(outPath);

        var reloaded = new ObjMesh();
        reloaded.ReadFile(outPath);

        reloaded.VertexColors.ShouldNotBeNull();
        reloaded.VertexColors.Length.ShouldBe(reloaded.Vertices.Length);

        // Every vertex in the re-loaded file should now have an explicit color
        // (because WriteFile wrote colors for all vertices)
        for (int i = 0; i < reloaded.VertexColors.Length; i++)
        {
            var c = reloaded.VertexColors[i];
            c.x.ShouldBeInRange(0f, 1f);
            c.y.ShouldBeInRange(0f, 1f);
            c.z.ShouldBeInRange(0f, 1f);
        }
    }

    #endregion

    #region Fix 2: Malformed alpha — TryParse resilience

    [Test]
    public void ReadFile_MalformedAlpha_DoesNotThrow()
    {
        // alpha-colors.obj: v2 has "notanumber" as alpha → should NOT throw
        // Before fix: float.Parse would throw FormatException
        // After fix: float.TryParse returns 0 as default (and doesn't crash)
        Should.NotThrow(() =>
        {
            var mesh = new ObjMesh();
            mesh.ReadFile(Path.Combine(TestDataPath, "alpha-colors.obj"));
        });
    }

    [Test]
    public void ReadFile_MalformedAlpha_ParsesOtherComponentsCorrectly()
    {
        var mesh = new ObjMesh();
        mesh.ReadFile(Path.Combine(TestDataPath, "alpha-colors.obj"));

        mesh.VertexColors.ShouldNotBeNull();
        mesh.VertexColors.Length.ShouldBe(mesh.Vertices.Length);

        // v1: (1, 0, 0, 0.5) — valid alpha
        // v2: (0, 1, 0, 0) — malformed alpha defaults to 0 via TryParse
        // v3: (0, 0, 1, 1) — no alpha field, defaults to 1

        // All RGB components should be parsed correctly regardless of alpha issues
        var hasRed = mesh.VertexColors.Any(c => Math.Abs(c.x - 1f) < 0.01f && Math.Abs(c.y) < 0.01f);
        var hasGreen = mesh.VertexColors.Any(c => Math.Abs(c.y - 1f) < 0.01f && Math.Abs(c.x) < 0.01f);
        var hasBlue = mesh.VertexColors.Any(c => Math.Abs(c.z - 1f) < 0.01f && Math.Abs(c.x) < 0.01f);

        hasRed.ShouldBeTrue("Red vertex RGB should be parsed correctly");
        hasGreen.ShouldBeTrue("Green vertex RGB should be parsed correctly");
        hasBlue.ShouldBeTrue("Blue vertex RGB should be parsed correctly");
    }

    #endregion

    #region Fix 3: Alpha round-trip preservation

    [Test]
    public void WriteRead_AlphaPreserved_WhenNotOne()
    {
        // alpha-colors.obj: v1 has alpha=0.5
        // After fix: WriteVertices writes alpha when != 1, so round-trip preserves it
        var testPath = GetTestOutputPath(nameof(WriteRead_AlphaPreserved_WhenNotOne));
        var mesh = new ObjMesh();
        mesh.ReadFile(Path.Combine(TestDataPath, "alpha-colors.obj"));

        var outPath = Path.Combine(testPath, "alpha-out.obj");
        mesh.WriteFile(outPath);

        var reloaded = new ObjMesh();
        reloaded.ReadFile(outPath);

        reloaded.VertexColors.ShouldNotBeNull();

        // Find the vertex that originally had alpha=0.5
        var v1Color = reloaded.VertexColors.FirstOrDefault(c =>
            Math.Abs(c.x - 1f) < 0.01f &&
            Math.Abs(c.y) < 0.01f &&
            Math.Abs(c.z) < 0.01f);

        v1Color.w.ShouldBe(0.5f, 0.01f, "Alpha=0.5 should survive write/read round-trip");
    }

    [Test]
    public void WriteRead_AlphaOmitted_WhenOne()
    {
        // Verify that alpha=1 is NOT written (standard OBJ format: v x y z r g b)
        var testPath = GetTestOutputPath(nameof(WriteRead_AlphaOmitted_WhenOne));
        var mesh = new ObjMesh();
        mesh.ReadFile(Path.Combine(TestDataPath, "alpha-colors.obj"));

        var outPath = Path.Combine(testPath, "alpha-omit-out.obj");
        mesh.WriteFile(outPath);

        // Read raw file and check that lines with alpha=1 have exactly 7 fields (v x y z r g b)
        // and lines with alpha!=1 have 8 fields (v x y z r g b a)
        var lines = File.ReadAllLines(outPath)
            .Where(l => l.StartsWith("v "))
            .ToArray();

        lines.Length.ShouldBeGreaterThan(0);

        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Should be 7 (v x y z r g b) or 8 (v x y z r g b a)
            parts.Length.ShouldBeOneOf(7, 8);

            if (parts.Length == 8)
            {
                // Only written when alpha != 1
                float.TryParse(parts[7], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var alpha);
                alpha.ShouldNotBe(1f, "Alpha=1 should not be written explicitly");
            }
        }
    }

    #endregion
}
