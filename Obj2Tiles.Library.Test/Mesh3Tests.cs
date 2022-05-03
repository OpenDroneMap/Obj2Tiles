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
    public void Cube_TrimTextures()
    {
        //var mesh = (MeshT)MeshUtils.LoadMesh("TestData/cube/cube.obj");
        var mesh = (MeshT)MeshUtils.LoadMesh(
            @"C:\datasets\drone_dataset_brighton_beach\odm_texturing\odm_textured_model_geo.obj");
        Directory.CreateDirectory("out");

        mesh.TrimTextures();
        /*
        var center = mesh.GetVertexBaricenter();
        
        mesh.Split(xutils, center.X, out var left, out var right);
        
        ((MeshT)left).TrimTextures();
        ((MeshT)right).TrimTextures();
        
        left.WriteObj("out/left.obj");
        right.WriteObj("out/right.obj");
   */
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

        /*CopyImage(image, newImage,
            new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2),
            new Point(0, 0));
*/
        Common.CopyImage(image, newImage, image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2, 0, 0);
        Common.CopyImage(image2, newImage, image2.Width / 4, image2.Height / 4, image2.Width / 2, image2.Height / 2
            , image.Width, 0);
        Common.CopyImage(image3, newImage, image3.Width / 4, image3.Height / 4, image3.Width / 2, image3.Height / 2
            , 0, image.Height);
        Common.CopyImage(image4, newImage, image4.Width / 4, image4.Height / 4, image4.Width / 2, image4.Height / 2
            , image.Width, image.Height);

        /*
               newImage.Mutate(x =>
               {
                  
                   x.Clip(new RectangularPolygon(0, 0, image.Width * 2, image.Height), ctx =>
                       ctx.DrawImage(image, new Point(image.Width, 0), 1f));
                   
                   x.Clip(new RectangularPolygon(0, 0, image2.Width, image2.Height), ctx =>
                       ctx.DrawImage(image2, new Point(0, 0), 1f));
       
               });*/

        image.Save("pic3-mod1.jpg");
        newImage.Save("newimage2.jpg");
    }


    [Test]
    public void Image_TextDrawTriangle()
    {
        var imagePath = "TestData/cube/pic5.jpg";

        using var image = Image.Load<Rgba32>(imagePath);
        using var newImage = new Image<Rgba32>(image.Width, image.Height);

        var a = new Vertex2(0.1, 0.1);
        var b = new Vertex2(0.2, 0.15);
        var c = new Vertex2(0.15, 0.2);
        var pos = new Vertex2(0, 0);

        CopyTriangle(image, newImage, a, b, c, pos);

        a = new Vertex2(0.5, 0.5);
        b = new Vertex2(0.6, 0.75);
        c = new Vertex2(0.85, 0.5);
        pos = new Vertex2(200, 0);

        CopyTriangle(image, newImage, a, b, c, pos);

        a = new Vertex2(0.3, 0.8);
        b = new Vertex2(0.5, 0.45);
        c = new Vertex2(0.65, 0.4);
        pos = new Vertex2(200, 200);

        CopyTriangle(image, newImage, a, b, c, pos);

        //seg.Save("pic2-mod2.jpg");

        //newImage.Mutate(o => o.DrawImage(image, new Point(0,0), 1f));

        // Crop triangle of image
        //image.Mutate(x => x.Crop(triangle));

        //image.Mutate(x => x.Clip(triangle, context => context.Save));
        //image.Mutate(x => x.Clip(polygon, context => context.));

        image.Save("pic2-mod1.jpg");
        newImage.Save("newimage.jpg");
    }

    private static void CopyTriangle(Image<Rgba32> image, Image<Rgba32> newImage, Vertex2 a, Vertex2 b, Vertex2 c,
        Vertex2 pos)
    {
        var aR = new PointF((float)a.X * image.Width, (float)a.Y * image.Height);
        var bR = new PointF((float)b.X * image.Width, (float)b.Y * image.Height);
        var cR = new PointF((float)c.X * image.Width, (float)c.Y * image.Height);

        var path = new Polygon(
            new LinearLineSegment(aR, bR),
            new LinearLineSegment(bR, cR),
            new LinearLineSegment(cR, aR)
        );

        using var seg = new Image<Rgba32>(image.Width, image.Height);

        seg.Mutate(x =>
        {
            x.Clip(path, context => context.DrawImage(image, new Point(0, 0), 1f));
            x.Crop(new Rectangle((int)path.Bounds.Left, (int)path.Bounds.Top, (int)path.Bounds.Width,
                (int)path.Bounds.Height));
        });

        newImage.Mutate(x => x.DrawImage(seg, new Point((int)pos.X, (int)pos.Y), 1f));
    }
}