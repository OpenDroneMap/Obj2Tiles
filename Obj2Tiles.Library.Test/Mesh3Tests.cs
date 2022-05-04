using System.Diagnostics;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace Obj2Tiles.Library.Test;

public class Mesh3Tests
{
    [SetUp]
    public void Setup()
    {
    }

    private static readonly IVertexUtils yutils = new VertexUtilsY();
    private static readonly IVertexUtils xutils = new VertexUtilsX();
    private static readonly IVertexUtils zutils = new VertexUtilsZ();

    [Test]
    public void WriteObj_Cube2_Repacking()
    {
        const string folder = nameof(WriteObj_Cube2_Repacking);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        
        var mesh = (MeshT)MeshUtils.LoadMesh("TestData/cube2/cube.obj");

        mesh.PreserveOriginalTextures = false;
        
        mesh.WriteObj(folder + "/mesh.obj");
    }
    
    [Test]
    public void WriteObj_Cube2_PreserveOriginalTextures()
    {
        const string folder = nameof(WriteObj_Cube2_PreserveOriginalTextures);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        
        var mesh = (MeshT)MeshUtils.LoadMesh("TestData/cube2/cube.obj");
        mesh.PreserveOriginalTextures = false;
        
        mesh.WriteObj(folder + "/mesh.obj");
    }
    
    [Test]
    public void WriteObj_Cube_Repacking()
    {
        const string folder = nameof(WriteObj_Cube_Repacking);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);

        var mesh = (MeshT)MeshUtils.LoadMesh("TestData/cube/cube.obj");

        mesh.PreserveOriginalTextures = false;
        
        mesh.WriteObj(folder + "/mesh.obj");
    }
    
    [Test]
    public void WriteObj_Cube_PreserveOriginalTextures()
    {
        const string folder = nameof(WriteObj_Cube_PreserveOriginalTextures);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        
        var mesh = (MeshT)MeshUtils.LoadMesh("TestData/cube/cube.obj");
        mesh.PreserveOriginalTextures = false;
        
        mesh.WriteObj(folder + "/mesh.obj");
    }
    
    [Test]
    public void WriteObj_Brighton_Repacking()
    {
        const string folder = nameof(WriteObj_Brighton_Repacking);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        
        var mesh = (MeshT)MeshUtils.LoadMesh(@"C:\datasets\drone_dataset_brighton_beach\odm_texturing\odm_textured_model_geo.obj");

        mesh.PreserveOriginalTextures = false;
        
        mesh.WriteObj(folder + "/mesh.obj");
    }
    
    [Test]
    public void WriteObj_Splitted_Cube_TrimTextures()
    {
        const string folder = nameof(WriteObj_Splitted_Cube_TrimTextures);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        
        var mesh = MeshUtils.LoadMesh("TestData/cube/cube.obj");

        var center = mesh.GetVertexBaricenter();
        
        mesh.Split(xutils, center.X, out var left, out var right);
        
        ((MeshT)left).PreserveOriginalTextures = false;
        ((MeshT)right).PreserveOriginalTextures = false;
        
        left.WriteObj(folder + "/left.obj");
        right.WriteObj(folder + "/right.obj");
   
        /*
        left.Split(yutils, center.Y, out var leftbottom, out var lefttop);
        right.Split(yutils, center.Y, out var rightbottom, out var righttop);

        leftbottom.Split(zutils, center.Z, out var leftbottomnear, out var leftbottomfar);
        lefttop.Split(zutils, center.Z, out var lefttopnear, out var lefttopfar);

        rightbottom.Split(zutils, center.Z, out var rightbottomnear, out var rightbottomfar);
        righttop.Split(zutils, center.Z, out var righttopnear, out var righttopfar);

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
        
        */
    }

    [Test]
    public void Image_TestDrawImage()
    {
        var imagePath = "TestData/cube/pic5.jpg";
        var imagePath2 = "TestData/cube/pic6.jpg";
        var imagePath3 = "TestData/cube/pic3.jpg";
        var imagePath4 = "TestData/cube/pic1.jpg";

        using var image = Image.Load<Rgba32>(imagePath);
        using var image2 = Image.Load<Rgba32>(imagePath2);
        using var image3 = Image.Load<Rgba32>(imagePath3);
        using var image4 = Image.Load<Rgba32>(imagePath4);

        using var newImage = new Image<Rgba32>(image.Width * 2, image.Height * 2);
        
        Common.CopyImage(image, newImage, 0, 0, image.Width  , image.Height, 0, 0);
        Common.CopyImage(image2, newImage, 0, 0, image2.Width, image2.Height, image.Width, 0);
        Common.CopyImage(image3, newImage, 0, 0, image3.Width, image3.Height, 0, image.Height);
        Common.CopyImage(image4, newImage, 0, 0, image4.Width, image4.Height, image.Width, image.Height);

        newImage.Save("collage.jpg");
    }

}