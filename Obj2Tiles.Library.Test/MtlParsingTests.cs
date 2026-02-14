using System.IO;
using System.Linq;
using NUnit.Framework;
using Obj2Tiles.Library.Materials;
using Shouldly;

namespace Obj2Tiles.Library.Test;

/// <summary>
/// Tests for Material.ReadMtl — the MTL parser.
/// Covers multi-material files, all property keywords, and edge cases.
/// </summary>
public class MtlParsingTests
{
    private const string TestDataPath = "TestData";

    [Test]
    public void ReadMtl_MultiMaterial_ParsesBothMaterials()
    {
        var materials = Material.ReadMtl(Path.Combine(TestDataPath, "multi-material.mtl"), out var deps);

        materials.Length.ShouldBe(2);
        materials[0].Name.ShouldBe("Red");
        materials[1].Name.ShouldBe("Blue");
    }

    [Test]
    public void ReadMtl_AllProperties_ParsedCorrectly()
    {
        var materials = Material.ReadMtl(Path.Combine(TestDataPath, "multi-material.mtl"), out _);

        // Red material
        var red = materials[0];
        red.AmbientColor.ShouldNotBeNull();
        red.AmbientColor!.R.ShouldBe(0.1, tolerance: 0.001);
        red.DiffuseColor.ShouldNotBeNull();
        red.DiffuseColor!.R.ShouldBe(0.8, tolerance: 0.001);
        red.SpecularColor.ShouldNotBeNull();
        red.SpecularExponent!.Value.ShouldBe(100.0);
        red.Dissolve!.Value.ShouldBe(1.0);
        red.IlluminationModel.ShouldBe(Materials.IlluminationModel.HighlightOn);

        // Blue material
        var blue = materials[1];
        blue.DiffuseColor.ShouldNotBeNull();
        blue.DiffuseColor!.B.ShouldBe(0.8, tolerance: 0.001);
        blue.SpecularExponent!.Value.ShouldBe(50.0);
        blue.Dissolve!.Value.ShouldBe(0.9, tolerance: 0.001);
    }

    [Test]
    public void ReadMtl_MaterialWithoutTexture_TextureIsNull()
    {
        var materials = Material.ReadMtl(Path.Combine(TestDataPath, "multi-material.mtl"), out var deps);

        materials[0].Texture.ShouldBeNull();
        materials[1].Texture.ShouldBeNull();
        deps.Length.ShouldBe(0); // no texture files as dependencies
    }

    [Test]
    public void ReadMtl_Tr_InvertsToDissolve()
    {
        // Create a temp MTL with Tr instead of d
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "newmtl TestMat\nTr 0.3\n");
            var materials = Material.ReadMtl(tempFile, out _);

            materials.Length.ShouldBe(1);
            materials[0].Dissolve!.Value.ShouldBe(0.7, tolerance: 0.001); // d = 1 - Tr
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ReadMtl_EmptyFile_ReturnsSingleEmptyMaterial()
    {
        // The parser always adds the last material on exit
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "newmtl Empty\n");
            var materials = Material.ReadMtl(tempFile, out _);

            materials.Length.ShouldBe(1);
            materials[0].Name.ShouldBe("Empty");
            materials[0].DiffuseColor.ShouldBeNull();
            materials[0].Texture.ShouldBeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ReadMtl_ToMtl_RoundTrip()
    {
        var materials = Material.ReadMtl(Path.Combine(TestDataPath, "multi-material.mtl"), out _);

        // Write and re-read
        var tempFile = Path.GetTempFileName();
        try
        {
            var content = string.Join("\n", materials.Select(m => m.ToMtl()));
            File.WriteAllText(tempFile, content);

            var reread = Material.ReadMtl(tempFile, out _);
            reread.Length.ShouldBe(2);
            reread[0].Name.ShouldBe("Red");
            reread[1].Name.ShouldBe("Blue");
            reread[0].SpecularExponent.ShouldBe(100.0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // --- DiffuseColor-only material ---

    [Test]
    public void ReadMtl_DiffuseColorOnly_KdParsed()
    {
        // Materials with only Kd (no map_Kd) should still have DiffuseColor set.
        // NOTE: Issue #36 (grey materials in glTF) is a downstream Converter.cs bug
        // and is NOT addressed by this PR. This test only validates the MTL parser.
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "newmtl ColorOnly\nKd 0.8 0.2 0.3\n");
            var materials = Material.ReadMtl(tempFile, out _);

            materials.Length.ShouldBe(1);
            materials[0].DiffuseColor.ShouldNotBeNull();
            materials[0].DiffuseColor!.R.ShouldBe(0.8, tolerance: 0.001);
            materials[0].DiffuseColor!.G.ShouldBe(0.2, tolerance: 0.001);
            materials[0].DiffuseColor!.B.ShouldBe(0.3, tolerance: 0.001);
            materials[0].Texture.ShouldBeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
