using System.IO;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles.Library.Test;

public class Mesh3DTests
{
    [SetUp]
    public void Setup()
    {
    }

    private static readonly IVertexUtils yutils = new VertexUtilsY();
    private static readonly IVertexUtils xutils = new VertexUtilsX();
    private static readonly IVertexUtils zutils = new VertexUtilsZ();

    [Test]
    public void Cube_TrimTextures()
    {
        var cube = new Mesh3("TestData/cube/cube.obj");

        cube.Split(xutils, 0, out var left, out var right);
        left.Split(yutils, 0, out var leftbottom, out var lefttop);
        right.Split(yutils, 0, out var rightbottom, out var righttop);

        leftbottom.Split(zutils, 0, out var leftbottomnear, out var leftbottomfar);
        lefttop.Split(zutils, 0, out var lefttopnear, out var lefttopfar);

        rightbottom.Split(zutils, 0, out var rightbottomnear, out var rightbottomfar);
        righttop.Split(zutils, 0, out var righttopnear, out var righttopfar);

        Directory.CreateDirectory("out");

        leftbottomnear.TrimTextures();
        leftbottomfar.TrimTextures();
        lefttopnear.TrimTextures();
        lefttopfar.TrimTextures();

        rightbottomnear.TrimTextures();
        rightbottomfar.TrimTextures();
        righttopnear.TrimTextures();
        righttopfar.TrimTextures();

        leftbottomnear.WriteObj("out/leftbottomnear.obj");
        leftbottomfar.WriteObj("out/leftbottomfar.obj");
        lefttopnear.WriteObj("out/lefttopnear.obj");
        lefttopfar.WriteObj("out/lefttopfar.obj");

        rightbottomnear.WriteObj("out/rightbottomnear.obj");
        rightbottomfar.WriteObj("out/rightbottomfar.obj");
        righttopnear.WriteObj("out/righttopnear.obj");
        righttopfar.WriteObj("out/righttopfar.obj");
    }
}