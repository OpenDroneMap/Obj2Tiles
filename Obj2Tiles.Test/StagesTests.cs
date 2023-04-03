using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Obj2Tiles.Common;
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

    [Test]
    public void TilingStage_TileCoords()
    {
        var gpsCoords = new GpsCoords
        {
            Altitude = 180,
            Latitude = 45.46424200394995,
            Longitude = 9.190277486808588
        };

        var transform = gpsCoords.ToEcefTransform();
        
        Console.WriteLine(JsonConvert.SerializeObject(transform, Formatting.Indented));

    }
    
    [Test]
    public void TilingStage_TileCoords2()
    {
        var gpsCoords = new GpsCoords
        {
            Altitude = 0,
            Latitude = 46.84265123717067,
            Longitude = -91.99400951340482
        };

        var transform = gpsCoords.ToEcefTransform();
        
        Console.WriteLine(JsonConvert.SerializeObject(transform, Formatting.Indented));

    }

    [Test]
    public void TilingStage_ConvertTest()
    {
        var testPath = GetTestOutputPath(nameof(TilingStage_ConvertTest));
        
        Utils.ConvertB3dm("TestData/Tile2/Mesh-XL-YR-XR-YL.obj", Path.Combine(testPath, "out.b3dm"));

    }

}