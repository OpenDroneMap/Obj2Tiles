using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using Path = System.IO.Path;

namespace Obj2Tiles.Library.Test;

public class Mesh3Tests
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

    private static readonly IVertexUtils yutils = new VertexUtilsY();
    private static readonly IVertexUtils xutils = new VertexUtilsX();
    private static readonly IVertexUtils zutils = new VertexUtilsZ();

    [Test]
    [Explicit]
    public void WriteObj_Cube2_Repacking()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Cube2_Repacking));

        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube2/cube.obj"));

        mesh.TexturesStrategy = TexturesStrategy.Repack;

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));
    }

    [Test]
    [Explicit]
    public void WriteObj_Cube2_PreserveOriginalTextures()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Cube2_PreserveOriginalTextures));

        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube2/cube.obj"));
        mesh.TexturesStrategy = TexturesStrategy.KeepOriginal;

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));
    }

    [Test]
    [Explicit]
    public void WriteObj_Cube_Repacking()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Cube_Repacking));

        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube/cube.obj"));

        mesh.TexturesStrategy = TexturesStrategy.Repack;

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));
    }

    [Test]
    [Explicit]
    public void WriteObj_Cube_PreserveOriginalTextures()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Cube_PreserveOriginalTextures));

        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube/cube.obj"));
        mesh.TexturesStrategy = TexturesStrategy.KeepOriginal;

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));
    }

    [Test]
    [Explicit]
    public void WriteObj_Brighton_Repacking()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Brighton_Repacking));

        var mesh = (MeshT)MeshUtils.LoadMesh(
            @"C:\datasets\drone_dataset_brighton_beach\odm_texturing\odm_textured_model_geo.obj");

        mesh.TexturesStrategy = TexturesStrategy.Repack;

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));
    }

    [Test]
    [Explicit]
    public void WriteObj_Canyon_Repacking()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Canyon_Repacking));

        var mesh = (MeshT)MeshUtils.LoadMesh(@"C:\datasets\canyon\odm_texturing\odm_textured_model_geo.obj");

        mesh.TexturesStrategy = TexturesStrategy.Repack;

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));
    }

    [Test]
    [Explicit]
    public void WriteObj_Splitted_Cube_PreserveOriginalTextures()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Splitted_Cube_PreserveOriginalTextures));

        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube/cube.obj"));

        var center = mesh.GetVertexBaricenter();

        mesh.Split(xutils, center.X, out var left, out var right);

        ((MeshT)left).TexturesStrategy = TexturesStrategy.KeepOriginal;
        ((MeshT)right).TexturesStrategy = TexturesStrategy.KeepOriginal;

        left.WriteObj(Path.Combine(testPath, "left.obj"));
        right.WriteObj(Path.Combine(testPath, "right.obj"));
    }

    [Test]
    [Explicit]
    public void WriteObj_SplittedX_Cube_Repacking()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_SplittedX_Cube_Repacking));

        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube/cube.obj"));

        var center = mesh.GetVertexBaricenter();

        mesh.Split(xutils, center.X, out var left, out var right);

        ((MeshT)left).TexturesStrategy = TexturesStrategy.Repack;
        ((MeshT)right).TexturesStrategy = TexturesStrategy.Repack;

        left.WriteObj(Path.Combine(testPath, "left.obj"));
        right.WriteObj(Path.Combine(testPath, "right.obj"));
    }

    [Test]
    [Explicit]
    public void WriteObj_SplittedX_Cube_PreserveOriginalTextures()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_SplittedX_Cube_PreserveOriginalTextures));

        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube/cube.obj"));

        var center = mesh.GetVertexBaricenter();

        mesh.Split(xutils, center.X, out var left, out var right);

        ((MeshT)left).TexturesStrategy = TexturesStrategy.KeepOriginal;
        ((MeshT)right).TexturesStrategy = TexturesStrategy.KeepOriginal;

        left.WriteObj(Path.Combine(testPath, "left.obj"));
        right.WriteObj(Path.Combine(testPath, "right.obj"));
    }

    [Test]
    [Explicit]
    public void WriteObj_Splitted_Cube2_PreserveOriginalTextures()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Splitted_Cube2_PreserveOriginalTextures));

        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube2/cube.obj"));

        var center = mesh.GetVertexBaricenter();

        mesh.Split(xutils, center.X, out var left, out var right);

        ((MeshT)left).TexturesStrategy = TexturesStrategy.KeepOriginal;
        ((MeshT)right).TexturesStrategy = TexturesStrategy.KeepOriginal;

        left.WriteObj(Path.Combine(testPath, "left.obj"));
        right.WriteObj(Path.Combine(testPath, "right.obj"));
    }

    [Test]
    [Explicit]
    public void WriteObj_Splitted_Cube2_Repacking()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Splitted_Cube2_Repacking));

        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube2/cube.obj"));

        var center = mesh.GetVertexBaricenter();

        mesh.Split(xutils, center.X, out var left, out var right);

        ((MeshT)left).TexturesStrategy = TexturesStrategy.Repack;
        ((MeshT)right).TexturesStrategy = TexturesStrategy.Repack;

        left.WriteObj(Path.Combine(testPath, "left.obj"));
        right.WriteObj(Path.Combine(testPath, "right.obj"));
    }

    [Test]
    public void Image_TestDrawImage()
    {
        var imagePath = "TestData/cube/pic5.jpg";
        var imagePath2 = "TestData/cube/pic6.jpg";
        var imagePath3 = "TestData/cube/pic3.jpg";
        var imagePath4 = "TestData/cube/pic1.jpg";

        var testPath = GetTestOutputPath(nameof(Image_TestDrawImage));

        using var image = Image.Load<Rgba32>(imagePath);
        using var image2 = Image.Load<Rgba32>(imagePath2);
        using var image3 = Image.Load<Rgba32>(imagePath3);
        using var image4 = Image.Load<Rgba32>(imagePath4);

        using var newImage = new Image<Rgba32>(image.Width * 2, image.Height * 2);

        Common.CopyImage(image, newImage, 0, 0, image.Width, image.Height, 0, 0);
        Common.CopyImage(image2, newImage, 0, 0, image2.Width, image2.Height, image.Width, 0);
        Common.CopyImage(image3, newImage, 0, 0, image3.Width, image3.Height, 0, image.Height);
        Common.CopyImage(image4, newImage, 0, 0, image4.Width, image4.Height, image.Width, image.Height);

        newImage.Save(Path.Combine(testPath, "collage.jpg"));
    }

    [Test]
    public void Image_TestHalfImage()
    {

        var testPath = GetTestOutputPath(nameof(Image_TestHalfImage));

        var sourcePath = Path.Combine(TestDataPath, "cube/pic1.jpg");
        using var image = Image.Load<Rgba32>(sourcePath);

        using var newImage = new Image<Rgba32>(image.Width, image.Height);

        Common.CopyImage(image, newImage, 0, 0, image.Width / 2, image.Height, 0, 0);
        newImage.SaveAsJpeg(Path.Combine(testPath, "out.jpg"));
    }
}