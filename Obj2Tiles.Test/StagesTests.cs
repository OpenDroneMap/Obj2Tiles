using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Stages;
using Obj2Tiles.Stages.Model;

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

        var boundsMapper = (from file in Directory.GetFiles("TestData/Tile1/LOD-0", "*.json")
            let bounds = JsonConvert.DeserializeObject<BoxDTO>(File.ReadAllText(file))
            select new
            {
                Bounds = new Box3(new Vertex3(bounds.Min.X, bounds.Min.Y, bounds.Min.Z),
                    new Vertex3(bounds.Max.X, bounds.Max.Y, bounds.Max.Z)),
                Name = Path.GetFileNameWithoutExtension(file)
            }).ToDictionary(item => item.Name, item => item.Bounds);
    
        StagesFacade.Tile("TestData/Tile1", testPath, 1, new[] { boundsMapper });
        
    }
}