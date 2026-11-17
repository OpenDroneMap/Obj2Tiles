using System.IO;
using NUnit.Framework;
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
