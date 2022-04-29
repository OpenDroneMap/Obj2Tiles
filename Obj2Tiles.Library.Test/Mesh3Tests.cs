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
        //var cube = new Mesh3("TestData/cube/cube.obj");
        var cube = (MeshT)MeshUtils.LoadMesh(@"C:\datasets\drone_dataset_brighton_beach\odm_texturing\odm_textured_model_geo.obj");
        Directory.CreateDirectory("out");

        var center = cube.GetVertexBaricenter();
        
        cube.Split(xutils, center.X, out var left, out var right);
        
        ((MeshT)left).TrimTextures();
        ((MeshT)right).TrimTextures();
        
        left.WriteObj("out/left.obj");
        right.WriteObj("out/right.obj");

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
    public void Image_TrimTextures()
    {
        var imagePath = "TestData/cube/pic5.jpg";

        using var image = Image.Load<Rgba32>(imagePath);
        /*
         *Relative Box: (0 0) - (0,5 0,5)
Box: (0 0) - (512 512)
         * 
         */

        var relativeBox = new Box2(0, 0, 0.5, 0.5);
        var box = relativeBox.Scale(image.Width, image.Height);

        //IPath polygon = new RectangularPolygon((float)box.Min.X, (float)box.Min.Y, (float)box.Max.X, (float)box.Max.Y);
/*        IPath triangle = new Polygon(
            new LinearLineSegment(new PointF(0, (float)image.Height), new PointF((float)image.Width / 2, 0)),
            new LinearLineSegment(new PointF((float)image.Width / 2, 0), new PointF((float)image.Width, image.Height)),
            new LinearLineSegment(new PointF(0, (float)image.Height), new PointF((float)image.Width, image.Height))
        );*/
        
        using var seg = new Image<Rgba32>(image.Width, image.Height);

        var a = new Vertex2(0.1, 0.1);
        var b = new Vertex2(0.2, 0.15);
        var c = new Vertex2(0.15, 0.2);
        
        var aR = new PointF((float)a.X * image.Width, (float)a.Y * image.Height);
        var bR = new PointF((float)b.X * image.Width, (float)b.Y * image.Height);
        var cR = new PointF((float)c.X * image.Width, (float)c.Y * image.Height);
        
        IPath path = new Polygon(
            new LinearLineSegment(aR, bR),
            new LinearLineSegment(bR, cR),
            new LinearLineSegment(cR, aR)
        );
        
        using var newImage = new Image<Rgba32>(image.Width, image.Height);

        /*
        using var img = new Image<Rgba32>((int)path.Bounds.Width, (int)path.Bounds.Height);
        img.Mutate(x => x.Clip(scaled, ctx => ctx.DrawImage(image, new Point(0, 0), 1f)));
        img.Save("seg.jpg");*/
        
        seg.Mutate(x =>
        {
            x.Clip(path, context => context.DrawImage(image, new Point(0,0), 1f));
            x.Crop(new Rectangle((int)path.Bounds.Left, (int)path.Bounds.Top, (int)path.Bounds.Width, (int)path.Bounds.Height));
        });
        
        newImage.Mutate(x => x.DrawImage(seg, new Point(0,0), 1f));
        
        //newImage.Mutate(o => o.DrawImage(image, new Point(0,0), 1f));
        
        // Crop triangle of image
        //image.Mutate(x => x.Crop(triangle));
        
        //image.Mutate(x => x.Clip(triangle, context => context.Save));
        //image.Mutate(x => x.Clip(polygon, context => context.));

        image.Save("pic2-mod1.jpg");
        seg.Save("pic2-mod2.jpg");
        newImage.Save("newimage.jpg");
    }
}