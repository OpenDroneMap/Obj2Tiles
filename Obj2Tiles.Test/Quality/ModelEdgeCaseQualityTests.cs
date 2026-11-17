using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shouldly;

namespace Obj2Tiles.Test.Quality;

/// <summary>
/// Valid OBJ models that exporters (Blender, Assimp) write and that Obj2Tiles must tile whole: parts without
/// texture coordinates next to textured ones, material names with spaces, textures of any size, vertex colours on
/// some objects only, texture coordinates on the last object only, faces before the first usemtl. Each runs
/// with the options of a KTX2 tiling (the --texture-format Ktx2 --ktx2-quality 192 --split-strategy VertexMedian
/// options of DroneDB), through the whole gate (inspector and official validator).
/// </summary>
[TestFixture]
[Category("QualityGate")]
public class ModelEdgeCaseQualityTests
{
    private const int Cells = 20;
    private const int GridTriangles = 2 * Cells * Cells;

    private static string Run(string caseId, QualityDataset dataset, int lods, int divisions)
    {
        var args = new List<string> { "--lods", lods.ToString(CultureInfo.InvariantCulture),
            "--divisions", divisions.ToString(CultureInfo.InvariantCulture), "--local",
            "--texture-format", "Ktx2", "--ktx2-quality", "192", "--split-strategy", "VertexMedian" };
        if (divisions > 0)
            args.Add("--octree");

        var expected = new QualityExpectations
        {
            Lods = lods, Divisions = divisions, Octree = divisions > 0, Local = true,
            TextureFormat = QualityTextureFormat.Ktx2
        };

        return CliQualityGateTests.RunValidateAndAssert(caseId, dataset, args, expected);
    }

    // The materials of the tiles of a level, without the "-<n>" suffix of the atlases a material is split into.
    private static string[] MaterialsOf(string output, string level) =>
        TilesetInspector.TrianglesByMaterial(output, level).Keys
            .Select(name => Regex.Replace(name, "-[0-9]+$", "")).Distinct().ToArray();

    private static void EveryLevelHasMaterials(string output, int lods, params string[] materials)
    {
        for (var lod = 0; lod < lods; lod++)
            MaterialsOf(output, $"LOD-{lod}").ShouldBe(materials, ignoreOrder: true, $"LOD-{lod}");
    }

    // A textured grid and, beside it, a grid of faces without texture coordinates; both materials have
    // spaces in their names (the second one two in a row).
    private static QualityDataset TexturedAndPlainParts() => QualityDataset.Generated("textured-and-plain",
        new ObjBuilder().Line("mtllib model.mtl")
            .Line("usemtl Body Paint").Grid(0, textured: true)
            .Line("usemtl Paint 1  Carmine").Grid(Cells + 1, textured: false)
            .ToString(),
        "newmtl Body Paint\nKd 1 1 1\nmap_Kd body.png\n\nnewmtl Paint 1  Carmine\nKd 0.6 0.05 0.1\n",
        ("body.png", 64, 64));

    [Test]
    public void FacesWithoutTextureCoordinates_andMaterialNamesWithSpaces_areInTheFinestLevel()
    {
        using var dataset = TexturedAndPlainParts();

        var output = Run("textured-and-plain", dataset, lods: 2, divisions: 0);

        var finest = TilesetInspector.TrianglesByMaterial(output, "LOD-0");
        finest.ShouldBe(new Dictionary<string, int> { ["Body Paint"] = GridTriangles, ["Paint 1  Carmine"] = GridTriangles },
            ignoreOrder: true);
    }

    [Test]
    public void FacesWithoutTextureCoordinates_andMaterialNamesWithSpaces_areInEverySplitTileLevel()
    {
        using var dataset = TexturedAndPlainParts();

        var output = Run("textured-and-plain-octree", dataset, lods: 3, divisions: 1);

        EveryLevelHasMaterials(output, 3, "Body Paint", "Paint 1  Carmine");
    }

    // The inspector checks that every KTX2 image is a whole number of 4x4 blocks.
    [TestCase(250, 250)]
    [TestCase(249, 250)]
    [TestCase(30, 2)]
    public void TextureOfAnySize_isAWholeNumberOfKtx2Blocks(int width, int height)
    {
        using var dataset = QualityDataset.Generated($"texture-{width}x{height}",
            new ObjBuilder().Line("mtllib model.mtl").Line("usemtl t").Grid(0, textured: true).ToString(),
            "newmtl t\nKd 1 1 1\nmap_Kd t.png\n", ("t.png", width, height));

        Run($"texture-{width}x{height}", dataset, lods: 1, divisions: 0);
    }

    // As Blender writes several objects: the first without material, texture coordinates or vertex colours,
    // the second with vertex colours, the third with a material and texture coordinates. The faces before the
    // first usemtl have the default material at every level, not the texture of the last object.
    [TestCase(1, 0)]
    [TestCase(3, 1)]
    public void ObjectsWithDifferentAttributes_areTiled(int lods, int divisions)
    {
        using var dataset = QualityDataset.Generated("mixed-objects",
            new ObjBuilder().Line("mtllib model.mtl")
                .Line("o Plain").Grid(0, textured: false)
                .Line("o Coloured").Grid(Cells + 1, textured: false, coloured: true)
                .Line("o Textured").Line("usemtl t").Grid(2 * (Cells + 1), textured: true)
                .ToString(),
            "newmtl t\nKd 1 1 1\nmap_Kd t.png\n", ("t.png", 64, 64));

        var output = Run($"mixed-objects-{lods}-{divisions}", dataset, lods, divisions);

        EveryLevelHasMaterials(output, lods, "default", "t");
        TilesetInspector.TrianglesByMaterial(output, "LOD-0").Values.Sum().ShouldBeGreaterThanOrEqualTo(3 * GridTriangles);
    }

    // As Blender writes objects without material when only the last one has texture coordinates: there is no
    // texture, so the model is tiled untextured, with every face.
    [Test]
    public void TextureCoordinatesWithoutMaterials_areTiledUntextured()
    {
        using var dataset = QualityDataset.Generated("uvs-without-materials",
            new ObjBuilder().Line("o Plain").Grid(0, textured: false)
                .Line("o Unwrapped").Grid(Cells + 1, textured: true)
                .ToString());

        var output = Run("uvs-without-materials", dataset, lods: 1, divisions: 0);

        TilesetInspector.TrianglesByMaterial(output, "LOD-0").Values.Sum().ShouldBe(2 * GridTriangles);
    }

    private sealed class ObjBuilder
    {
        private readonly StringBuilder _obj = new();
        private int _vertices;
        private int _textureVertices;

        public ObjBuilder Line(string line)
        {
            _obj.Append(line).Append('\n');
            return this;
        }

        // A flat grid of Cells x Cells quads (two triangles each) starting at x = x0.
        public ObjBuilder Grid(double x0, bool textured, bool coloured = false)
        {
            var first = _vertices + 1;
            var firstUv = _textureVertices + 1;

            for (var j = 0; j <= Cells; j++)
            for (var i = 0; i <= Cells; i++)
            {
                _obj.Append(FormattableString.Invariant($"v {x0 + i} {j} {0.1 * ((i + j) % 2)}"));
                if (coloured)
                    _obj.Append(FormattableString.Invariant($" {(double)i / Cells} {(double)j / Cells} 0.5"));
                _obj.Append('\n');

                if (textured)
                    _obj.Append(FormattableString.Invariant($"vt {(double)i / Cells} {(double)j / Cells}\n"));
            }

            _vertices += (Cells + 1) * (Cells + 1);
            if (textured)
                _textureVertices += (Cells + 1) * (Cells + 1);

            string Corner(int i, int j)
            {
                var offset = j * (Cells + 1) + i;
                return textured ? $"{first + offset}/{firstUv + offset}" : $"{first + offset}";
            }

            for (var j = 0; j < Cells; j++)
            for (var i = 0; i < Cells; i++)
            {
                _obj.Append($"f {Corner(i, j)} {Corner(i + 1, j)} {Corner(i + 1, j + 1)}\n");
                _obj.Append($"f {Corner(i, j)} {Corner(i + 1, j + 1)} {Corner(i, j + 1)}\n");
            }

            return this;
        }

        public override string ToString() => _obj.ToString();
    }
}
