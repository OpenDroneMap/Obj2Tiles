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
using Shouldly;

namespace Obj2Tiles.Test;

public class StagesTests
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

        StagesFacade.Tile("TestData/Tile1", testPath, 1, 100, [boundsMapper]);

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

    #region GpsCoords / ECEF Tests

    [Test]
    public void ToEcefTransform_ScaleShouldNotAffectEcefPosition()
    {
        // The ECEF position (translation column) must be identical regardless of scale.
        // Scale should only affect local geometry, not the position on the globe.
        var coords1 = new GpsCoords(45.0, 9.0, 17.0, 1.0, false);
        var coords100 = new GpsCoords(45.0, 9.0, 17.0, 100.0, false);

        var t1 = coords1.ToEcefTransform();
        var t100 = coords100.ToEcefTransform();

        // Column-major: translation is at indices 12, 13, 14
        t1[12].ShouldBe(t100[12], 0.1, "ECEF X position should not change with scale");
        t1[13].ShouldBe(t100[13], 0.1, "ECEF Y position should not change with scale");
        t1[14].ShouldBe(t100[14], 0.1, "ECEF Z position should not change with scale");
    }

    [Test]
    public void ToEcefTransform_AltitudeIsIndependentOfScale()
    {
        // With alt=17, scale=100, the position should reflect alt=17m, NOT alt=1700m.
        var coordsNoScale = new GpsCoords(45.0, 9.0, 17.0, 1.0, false);
        var coordsScaled = new GpsCoords(45.0, 9.0, 17.0, 100.0, false);

        var tNo = coordsNoScale.ToEcefTransform();
        var tSc = coordsScaled.ToEcefTransform();

        // ECEF positions must be the same (altitude not multiplied by scale)
        var dx = Math.Abs(tNo[12] - tSc[12]);
        var dy = Math.Abs(tNo[13] - tSc[13]);
        var dz = Math.Abs(tNo[14] - tSc[14]);

        var dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        dist.ShouldBeLessThan(0.01, "Position should not shift when changing scale");
    }

    [Test]
    public void ToEcefTransform_ScaleOnlyAffectsRotationColumns()
    {
        // The rotation/scale part of the matrix (first 3 columns, rows 0-2) should
        // differ by exactly the scale factor between scale=1 and scale=S.
        var coords1 = new GpsCoords(45.0, 9.0, 0.0, 1.0, false);
        var coords5 = new GpsCoords(45.0, 9.0, 0.0, 5.0, false);

        var t1 = coords1.ToEcefTransform();
        var t5 = coords5.ToEcefTransform();

        // Column-major: first 3 elements of each of the first 3 columns carry rotation*scale
        // Indices: col0=[0,1,2], col1=[4,5,6], col2=[8,9,10]
        int[] rotIndices = [0, 1, 2, 4, 5, 6, 8, 9, 10];
        foreach (var i in rotIndices)
        {
            if (Math.Abs(t1[i]) > 1e-10)
            {
                var ratio = t5[i] / t1[i];
                ratio.ShouldBe(5.0, 1e-6, $"Rotation element [{i}] should scale by factor 5");
            }
        }
    }

    [Test]
    public void ToEcefTransform_ZeroLatLon_PositionOnEquatorPrimeMeridian()
    {
        // At lat=0, lon=0, alt=0 the ECEF position should be (a, 0, 0)
        // where a = 6378137.0 (WGS84 semi-major axis)
        var coords = new GpsCoords(0, 0, 0, 1.0, false);
        var t = coords.ToEcefTransform();

        t[12].ShouldBe(6378137.0, 1.0, "X should be ~semi-major axis");
        t[13].ShouldBe(0.0, 1.0, "Y should be ~0");
        t[14].ShouldBe(0.0, 1.0, "Z should be ~0");
    }

    [Test]
    public void ToEcefTransform_NorthPole_PositionOnZAxis()
    {
        // At lat=90, lon=0, alt=0 the position should be (0, 0, b) where b ~ 6356752
        var coords = new GpsCoords(90, 0, 0, 1.0, false);
        var t = coords.ToEcefTransform();

        Math.Abs(t[12]).ShouldBeLessThan(1.0, "X should be ~0 at North Pole");
        Math.Abs(t[13]).ShouldBeLessThan(1.0, "Y should be ~0 at North Pole");
        t[14].ShouldBe(6356752.3142, 1.0, "Z should be ~semi-minor axis at North Pole");
    }

    [Test]
    public void ToEcefTransform_AltitudeIncreasesDistance()
    {
        var coordsGround = new GpsCoords(45.0, 9.0, 0, 1.0, false);
        var coordsHigh = new GpsCoords(45.0, 9.0, 1000.0, 1.0, false);

        var tG = coordsGround.ToEcefTransform();
        var tH = coordsHigh.ToEcefTransform();

        var distGround = Math.Sqrt(tG[12] * tG[12] + tG[13] * tG[13] + tG[14] * tG[14]);
        var distHigh = Math.Sqrt(tH[12] * tH[12] + tH[13] * tH[13] + tH[14] * tH[14]);

        (distHigh - distGround).ShouldBe(1000.0, 1.0, "1000m altitude should increase distance by ~1000m");
    }

    [Test]
    public void ToEcefTransform_NegativeAltitude()
    {
        var coordsGround = new GpsCoords(45.0, 9.0, 0, 1.0, false);
        var coordsBelow = new GpsCoords(45.0, 9.0, -100.0, 1.0, false);

        var tG = coordsGround.ToEcefTransform();
        var tB = coordsBelow.ToEcefTransform();

        var distGround = Math.Sqrt(tG[12] * tG[12] + tG[13] * tG[13] + tG[14] * tG[14]);
        var distBelow = Math.Sqrt(tB[12] * tB[12] + tB[13] * tB[13] + tB[14] * tB[14]);

        distBelow.ShouldBeLessThan(distGround, "Negative altitude should be closer to center");
        (distGround - distBelow).ShouldBe(100.0, 1.0);
    }

    [Test]
    public void ToEcefTransform_YUpToZUp_AppliesRotation()
    {
        var coordsNoRot = new GpsCoords(45.0, 9.0, 0, 1.0, false);
        var coordsRot = new GpsCoords(45.0, 9.0, 0, 1.0, true);

        var tN = coordsNoRot.ToEcefTransform();
        var tR = coordsRot.ToEcefTransform();

        // Transforms should differ (rotation applied)
        var differs = false;
        for (var i = 0; i < 16; i++)
            if (Math.Abs(tN[i] - tR[i]) > 1e-6)
                differs = true;

        differs.ShouldBeTrue("YUpToZUp should produce a different transform");

        // Position (translation) should be the same — rotation doesn't change origin
        tN[12].ShouldBe(tR[12], 0.1, "ECEF X should not change with Y-up-to-Z-up");
        tN[13].ShouldBe(tR[13], 0.1, "ECEF Y should not change with Y-up-to-Z-up");
        tN[14].ShouldBe(tR[14], 0.1, "ECEF Z should not change with Y-up-to-Z-up");
    }

    [Test]
    public void ToEcefTransform_MatrixIs16Elements()
    {
        var coords = new GpsCoords(45.0, 9.0, 100.0, 1.0, false);
        var t = coords.ToEcefTransform();
        t.Length.ShouldBe(16);
    }

    [Test]
    public void ToEcefTransform_LastRowIsIdentity()
    {
        // Column-major: last row is indices 3, 7, 11, 15 → should be 0, 0, 0, 1
        var coords = new GpsCoords(45.0, 9.0, 100.0, 2.0, false);
        var t = coords.ToEcefTransform();

        t[3].ShouldBe(0.0, 1e-10);
        t[7].ShouldBe(0.0, 1e-10);
        t[11].ShouldBe(0.0, 1e-10);
        t[15].ShouldBe(1.0, 1e-10);
    }

    [Test]
    public void ToEcefTransform_SouthernHemisphere()
    {
        // Lat = -33.86 (Sydney), lon = 151.21
        var coords = new GpsCoords(-33.86, 151.21, 0, 1.0, false);
        var t = coords.ToEcefTransform();

        // Z should be negative in southern hemisphere
        t[14].ShouldBeLessThan(0, "Z should be negative for southern hemisphere");
    }

    [Test]
    public void ToEcefTransform_Lon180_AntimeridianPosition()
    {
        // lon=180 should place on the antimeridian: X negative, Y ~0
        var coords = new GpsCoords(0, 180, 0, 1.0, false);
        var t = coords.ToEcefTransform();

        t[12].ShouldBeLessThan(0, "X should be negative at lon=180");
        Math.Abs(t[13]).ShouldBeLessThan(1.0, "Y should be ~0 at lon=180");
    }

    [Test]
    public void MultiplyMatrix_Identity()
    {
        double[] identity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        double[] m = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

        var result = GpsCoords.MultiplyMatrix(identity, m);
        for (var i = 0; i < 16; i++)
            result[i].ShouldBe(m[i], 1e-10);
    }

    [Test]
    public void ConvertToColumnMajorOrder_Roundtrip()
    {
        double[] m = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        var colMajor = GpsCoords.ConvertToColumnMajorOrder(m);
        var back = GpsCoords.ConvertToColumnMajorOrder(colMajor);

        for (var i = 0; i < 16; i++)
            back[i].ShouldBe(m[i], 1e-10);
    }

    #endregion

    #region TilingStage LocalMode Tests

    [Test]
    public void TilingStage_LocalMode_ProducesIdentityTransform()
    {
        var testPath = GetTestOutputPath(nameof(TilingStage_LocalMode_ProducesIdentityTransform));

        var boundsMapper = (from file in Directory.GetFiles("TestData/Tile1/LOD-0", "*.json")
            let bounds = JsonConvert.DeserializeObject<BoxDTO>(File.ReadAllText(file))
            select new
            {
                Bounds = new Box3(new Vertex3(bounds.Min.X, bounds.Min.Y, bounds.Min.Z),
                    new Vertex3(bounds.Max.X, bounds.Max.Y, bounds.Max.Z)),
                Name = Path.GetFileNameWithoutExtension(file)
            }).ToDictionary(item => item.Name, item => item.Bounds);

        StagesFacade.Tile("TestData/Tile1", testPath, 1, 100, [boundsMapper], localMode: true);

        var tilesetJson = File.ReadAllText(Path.Combine(testPath, "tileset.json"));
        var tileset = JsonConvert.DeserializeObject<Tileset>(tilesetJson);

        var transform = tileset!.Root!.Transform!;
        double[] identity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

        for (var i = 0; i < 16; i++)
            transform[i].ShouldBe(identity[i], 1e-10, $"Transform[{i}] should match identity");
    }

    [Test]
    public void TilingStage_DefaultCoords_NotIdentity()
    {
        var testPath = GetTestOutputPath(nameof(TilingStage_DefaultCoords_NotIdentity));

        var boundsMapper = (from file in Directory.GetFiles("TestData/Tile1/LOD-0", "*.json")
            let bounds = JsonConvert.DeserializeObject<BoxDTO>(File.ReadAllText(file))
            select new
            {
                Bounds = new Box3(new Vertex3(bounds.Min.X, bounds.Min.Y, bounds.Min.Z),
                    new Vertex3(bounds.Max.X, bounds.Max.Y, bounds.Max.Z)),
                Name = Path.GetFileNameWithoutExtension(file)
            }).ToDictionary(item => item.Name, item => item.Bounds);

        // Without localMode and without coords → default Milan coordinates → NOT identity
        StagesFacade.Tile("TestData/Tile1", testPath, 1, 100, [boundsMapper]);

        var tilesetJson = File.ReadAllText(Path.Combine(testPath, "tileset.json"));
        var tileset = JsonConvert.DeserializeObject<Tileset>(tilesetJson);

        var transform = tileset!.Root!.Transform!;

        // Translation column should be large ECEF values (millions of meters)
        var dist = Math.Sqrt(transform[12] * transform[12] +
                             transform[13] * transform[13] +
                             transform[14] * transform[14]);
        dist.ShouldBeGreaterThan(6000000, "Default coords should produce ECEF position on Earth's surface");
    }

    #endregion


    #region Octree / PathTraversal

    [Test]
    public void TilingStage_Octree_BuildsParentChildHierarchy()
    {
        var testPath = GetTestOutputPath(nameof(TilingStage_Octree_BuildsParentChildHierarchy));
        var src = Path.Combine(testPath, "src");
        void Tri(string lod, string name)
        {
            var d = Path.Combine(src, lod);
            Directory.CreateDirectory(d);
            File.WriteAllText(Path.Combine(d, name + ".obj"), "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        }
        Tri("LOD-0", "Mesh-XL-XR");
        Tri("LOD-1", "Mesh-XL");

        var lod0 = new Dictionary<string, Box3> { ["Mesh-XL-XR"] = new Box3(0, 0, 0, 1, 1, 1) };
        var lod1 = new Dictionary<string, Box3> { ["Mesh-XL"] = new Box3(0, 0, 0, 2, 2, 2) };

        StagesFacade.Tile(src, testPath, 2, 100, [lod0, lod1], localMode: true, isOctree: true);

        var tileset = JsonConvert.DeserializeObject<Tileset>(File.ReadAllText(Path.Combine(testPath, "tileset.json")));
        tileset!.Root!.Children.ShouldNotBeNull();
        tileset.Root.Children!.Count.ShouldBe(1);
        var coarse = tileset.Root.Children[0];
        coarse.Content!.Uri.ShouldBe("LOD-1/Mesh-XL.b3dm");
        coarse.Children.ShouldNotBeNull();
        coarse.Children!.Count.ShouldBe(1);
        coarse.Children[0].Content!.Uri.ShouldBe("LOD-0/Mesh-XL-XR.b3dm");
    }

    [Test]
    public void CopyObjDependencies_PathTraversal_DoesNotEscapeOutput()
    {
        var testPath = GetTestOutputPath(nameof(CopyObjDependencies_PathTraversal_DoesNotEscapeOutput));
        var inDir = Path.Combine(testPath, "in");
        var outDir = Path.Combine(testPath, "out");
        Directory.CreateDirectory(inDir);
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(inDir, "evil.mtl"), "newmtl X\n");
        File.WriteAllText(Path.Combine(inDir, "model.obj"), "mtllib ../evil.mtl\n");

        Obj2Tiles.Utils.CopyObjDependencies(Path.Combine(inDir, "model.obj"), outDir);

        File.Exists(Path.Combine(testPath, "evil.mtl")).ShouldBeFalse("traversal must not write outside output");
    }

    #endregion

}
