using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Obj2Tiles.Stages;

namespace Obj2Tiles.Test;

public class StagesTests
{
    private const string TestDataPath = "TestData";
    private const string TestOutputPath = "TestOutput";

    private string GetTestOutputPath(string testName)
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

    [Test]
    public void TilingStage_Tile()
    {
        var testPath = GetTestOutputPath(nameof(TilingStage_Tile));

        StagesFacade.Tile("TestData/Tile1", testPath, 1);
        
    }
}