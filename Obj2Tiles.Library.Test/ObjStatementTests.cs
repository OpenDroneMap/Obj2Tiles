using System.IO;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Library.Materials;
using Shouldly;

namespace Obj2Tiles.Library.Test;

/// <summary>
/// The keyword and argument of OBJ and MTL statements: names (usemtl, newmtl, mtllib) run to the end of the line.
/// </summary>
public class ObjStatementTests
{
    [TestCase("usemtl Paint 1 Carmine", "usemtl", "Paint 1 Carmine")]
    [TestCase("  usemtl   Paint 1  Carmine  ", "usemtl", "Paint 1  Carmine")]
    [TestCase("newmtl\tBand Carbon Fiber\r", "newmtl", "Band Carbon Fiber")]
    [TestCase("usemtl", "usemtl", "")]
    [TestCase("mtllib my model.mtl", "mtllib", "my model.mtl")]
    public void Split_TheArgumentIsTheRestOfTheLine(string line, string keyword, string argument)
    {
        ObjStatement.Split(line).ShouldBe((keyword, argument));
        ObjStatement.Argument(line).ShouldBe(argument);
    }

    [TestCase("v 1 2 3", new[] { "v", "1", "2", "3" })]
    [TestCase("\tmtllib model.mtl\r", new[] { "mtllib", "model.mtl" })]
    [TestCase("  f\t1/1  2/2\t\t3/3 ", new[] { "f", "1/1", "2/2", "3/3" })]
    [TestCase(" \t \r", new string[0])]
    public void Tokens_AreSeparatedBySpacesTabsOrLineEnds(string line, string[] tokens)
    {
        ObjStatement.Tokens(line).ShouldBe(tokens);
    }

    [Test]
    public void LoadMesh_StatementsSeparatedByTabs_AreRead()
    {
        var folder = Path.Combine(Path.GetTempPath(), "obj2tiles-tabs-" + Path.GetRandomFileName());
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "model.mtl"), "newmtl A\r\nKd\t0 1\t0\r\n");
            var obj = Path.Combine(folder, "model.obj");
            File.WriteAllText(obj,
                "mtllib\tmodel.mtl\r\nv\t0\t0\t0\r\nv 1\t0 0\r\nv 0 1 0\r\n" +
                "vt\t0 0\r\nvt 1\t0\r\nvt 0 1\r\nusemtl\tA\r\nf\t1/1\t2/2 3/3\r\n");

            var mesh = MeshUtils.LoadMesh(obj);

            mesh.ShouldBeOfType<MeshT>();
            mesh.FacesCount.ShouldBe(1);
            ((MeshT)mesh).Materials[0].DiffuseColor!.G.ShouldBe(1);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public void ReadMtl_MaterialNamesWithSpacesOrAfterATab_AreWholeNames()
    {
        var path = Path.Combine(Path.GetTempPath(), "obj2tiles-names-" + Path.GetRandomFileName() + ".mtl");
        try
        {
            File.WriteAllText(path, "newmtl Paint 1 Carmine\nKd 1 0 0\n\nnewmtl\tBezel  Frame\nKd 0 1 0\n");

            var materials = Material.ReadMtl(path, out _);

            materials.Length.ShouldBe(2);
            materials[0].Name.ShouldBe("Paint 1 Carmine");
            materials[1].Name.ShouldBe("Bezel  Frame");
            materials[1].DiffuseColor!.G.ShouldBe(1);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
