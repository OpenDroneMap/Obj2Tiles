using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Obj2Tiles.Common;
using Obj2Tiles.Library.Geometry;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using Path = System.IO.Path;

namespace Obj2Tiles.Library.Test;

public class Mesh3Tests
{
    private const string TestDataPath = "TestData";
    private const string TestOutputPath = "TestOutput";
    private const string BrightonTexturingTestUrl = "https://github.com/DroneDB/test_data/raw/master/brighton/odm_texturing.zip";

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

    private static readonly IVertexUtils yutils = new VertexUtilsY();
    private static readonly IVertexUtils xutils = new VertexUtilsX();
    private static readonly IVertexUtils zutils = new VertexUtilsZ();

    [Test]
    public void WriteObj_Square_RemoveUnused()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Square_RemoveUnused));

        var mesh = MeshUtils.LoadMesh(Path.Combine(TestDataPath, "square-unused.obj"));
        
        mesh.WriteObj(Path.Combine(testPath, "square.obj"));
    }
    
    [Test]
    public void WriteObj_Cube2_Repacking()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Cube2_Repacking));

        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube2/cube.obj"));

        mesh.TexturesStrategy = TexturesStrategy.Repack;

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));
    }

    [Test]
    public void WriteObj_Cube2_PreserveOriginalTextures()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Cube2_PreserveOriginalTextures));

        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube2/cube.obj"));
        mesh.TexturesStrategy = TexturesStrategy.KeepOriginal;

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));
    }

    [Test]
    public void WriteObj_Cube_Repacking()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Cube_Repacking));

        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube/cube.obj"));

        mesh.TexturesStrategy = TexturesStrategy.Repack;

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));
    }

    [Test]
    public void WriteObj_Cube_PreserveOriginalTextures()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Cube_PreserveOriginalTextures));

        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube/cube.obj"));
        mesh.TexturesStrategy = TexturesStrategy.KeepOriginal;

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));
    }

    [Test]
    public void WriteObj_Cube_SingleMaterialPerPart_ProducesReloadableSingleMaterialAtlas()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Cube_SingleMaterialPerPart_ProducesReloadableSingleMaterialAtlas));
        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube", "cube.obj"));
        mesh.TexturesStrategy = TexturesStrategy.Repack;
        mesh.SingleMaterialPerPart = true;

        var outputPath = Path.Combine(testPath, "mesh.obj");
        mesh.WriteObj(outputPath);

        var output = (MeshT)MeshUtils.LoadMesh(outputPath);
        output.Materials.Count.ShouldBe(1);
        output.Faces.All(face => face.MaterialIndex == 0).ShouldBeTrue();
        File.Exists(Path.Combine(testPath, output.Materials[0].Texture!)).ShouldBeTrue();
    }

    [Test]
    public void WriteObj_Cube_SingleMaterialPerPart_SubPixelChartsFitWithoutPadding()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_Cube_SingleMaterialPerPart_SubPixelChartsFitWithoutPadding));
        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube", "cube.obj"));
        mesh.SingleMaterialPerPart = true;
        mesh.MaxTextureSize = 4;

        var outputPath = Path.Combine(testPath, "mesh.obj");
        mesh.WriteObj(outputPath);

        var output = (MeshT)MeshUtils.LoadMesh(outputPath);
        output.Materials.Count.ShouldBe(1);
        output.Faces.All(face => face.MaterialIndex == 0).ShouldBeTrue();
    }

    [Test]
    public void WriteObj_SingleMaterialPerPart_SubPixelChartsDoNotReservePadding()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_SingleMaterialPerPart_SubPixelChartsDoNotReservePadding));
        var texturePath = Path.Combine(testPath, "source.png");
        using (var texture = new Image<Rgba32>(256, 256, new Rgba32(255, 0, 0, 255)))
            texture.SaveAsPng(texturePath);

        const int chartCount = 64;
        var vertices = new[] { new Vertex3(0, 0, 0), new Vertex3(1, 0, 0), new Vertex3(0, 1, 0) };
        var textureVertices = new List<Vertex2>(chartCount * 3);
        var faces = new List<FaceT>(chartCount);
        for (var i = 0; i < chartCount; i++)
        {
            var textureIndex = textureVertices.Count;
            var u = i * 0.015;
            textureVertices.Add(new Vertex2(u, 0));
            textureVertices.Add(new Vertex2(u + 0.01, 0));
            textureVertices.Add(new Vertex2(u, 0.01));
            faces.Add(new FaceT(0, 1, 2, textureIndex, textureIndex + 1, textureIndex + 2, 0));
        }

        var mesh = new MeshT(vertices, textureVertices, faces, [new Materials.Material("tiny", texturePath)])
        {
            TexturesStrategy = TexturesStrategy.Repack,
            SingleMaterialPerPart = true,
            MaxTextureSize = 32
        };

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));

        using var atlas = Image.Load(Path.Combine(testPath, mesh.Materials[0].Texture!));
        atlas.Width.ShouldBe(32);
        atlas.Height.ShouldBe(32);
    }

    [Test]
    public void WriteObj_SingleMaterialPerPart_RotatedCharts_KeepSourceContent()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_SingleMaterialPerPart_RotatedCharts_KeepSourceContent));
        var texturePath = Path.Combine(testPath, "grad.png");

        // Non-square source texture with a distinct color per pixel region.
        const int srcW = 128, srcH = 32;
        using (var tex = new Image<Rgba32>(srcW, srcH))
        {
            tex.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < srcH; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < srcW; x++)
                        row[x] = new Rgba32((byte)(x * 2 % 256), (byte)(y * 8 % 256), 128, 255);
                }
            });
            tex.SaveAsPng(texturePath);
        }

        // Anisotropic UV boxes to provoke 90-degree rotations in the packer.
        var boxes = new[]
        {
            new RectangleF(0.02f, 0.05f, 0.18f, 0.90f),
            new RectangleF(0.25f, 0.30f, 0.70f, 0.20f),
            new RectangleF(0.40f, 0.60f, 0.55f, 0.35f),
            new RectangleF(0.60f, 0.02f, 0.10f, 0.25f),
            new RectangleF(0.75f, 0.55f, 0.22f, 0.44f),
        };

        var vertices = new List<Vertex3>();
        var textureVertices = new List<Vertex2>();
        var faces = new List<FaceT>();
        var faceVertexUv = new List<(Vertex2 A, Vertex2 B, Vertex2 C)>();
        foreach (var b in boxes)
        {
            int vi = vertices.Count;
            vertices.Add(new Vertex3(0, 0, 0));
            vertices.Add(new Vertex3(1, 0, 0));
            vertices.Add(new Vertex3(0, 1, 0));
            int ti = textureVertices.Count;
            textureVertices.Add(new Vertex2(b.Left, b.Top));
            textureVertices.Add(new Vertex2(b.Left + b.Width, b.Top));
            textureVertices.Add(new Vertex2(b.Left, b.Top + b.Height));
            faces.Add(new FaceT(vi, vi + 1, vi + 2, ti, ti + 1, ti + 2, 0));
            faceVertexUv.Add((textureVertices[ti], textureVertices[ti + 1], textureVertices[ti + 2]));
        }

        var mesh = new MeshT(vertices, textureVertices, faces, [new Materials.Material("grad", texturePath)])
        {
            TexturesStrategy = TexturesStrategy.Repack,
            SingleMaterialPerPart = true,
            MaxTextureSize = 512
        };

        mesh.WriteObj(Path.Combine(testPath, "out.obj"));

        using var atlas = Image.Load<Rgba32>(Path.Combine(testPath, mesh.Materials[0].Texture!));
        int edge = atlas.Width;
        edge.ShouldBe(atlas.Height);

        int failures = 0;
        for (int fi = 0; fi < mesh.Faces.Count; fi++)
        {
            var face = mesh.Faces[fi];
            var orig = faceVertexUv[fi];
            var pairs = new[]
            {
                (orig.A, mesh.TextureVertices[face.TextureIndexA]),
                (orig.B, mesh.TextureVertices[face.TextureIndexB]),
                (orig.C, mesh.TextureVertices[face.TextureIndexC]),
            };
            foreach (var (o, n) in pairs)
            {
                int sx = Math.Clamp((int)Math.Round(o.X * srcW), 0, srcW - 1);
                int sy = Math.Clamp((int)Math.Round((1 - o.Y) * srcH), 0, srcH - 1);
                int ax = Math.Clamp((int)Math.Round(n.X * edge), 0, edge - 1);
                int ay = Math.Clamp((int)Math.Round((1 - n.Y) * edge), 0, edge - 1);

                byte expR = (byte)(sx * 2 % 256);
                byte expG = (byte)(sy * 8 % 256);
                var got = atlas[ax, ay];
                if (Math.Abs(got.R - expR) > 16 || Math.Abs(got.G - expG) > 16)
                    failures++;
            }
        }

        failures.ShouldBe(0, "atlas content must match source colors at remapped UVs");
    }

    [Test]
    public void WriteObj_SingleMaterialPerPart_NonSquareTexture_KeepsPerAxisDensity()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_SingleMaterialPerPart_NonSquareTexture_KeepsPerAxisDensity));
        var texturePath = Path.Combine(testPath, "stripes.png");

        // 1px-wide black/white vertical stripes: they average to gray if the U axis is downscaled.
        const int srcW = 128, srcH = 16;
        using (var tex = new Image<Rgba32>(srcW, srcH))
        {
            tex.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < srcH; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < srcW; x++)
                        row[x] = x / 2 % 2 == 0 ? new Rgba32(0, 0, 0, 255) : new Rgba32(255, 255, 255, 255);
                }
            });
            tex.SaveAsPng(texturePath);
        }

        var vertices = new[] { new Vertex3(0, 0, 0), new Vertex3(1, 0, 0), new Vertex3(0, 1, 0) };
        var textureVertices = new List<Vertex2> { new(0, 0), new(1, 0), new(0, 1) };
        var faces = new List<FaceT> { new(0, 1, 2, 0, 1, 2, 0) };

        var mesh = new MeshT(vertices, textureVertices, faces, [new Materials.Material("stripes", texturePath)])
        {
            TexturesStrategy = TexturesStrategy.Repack,
            SingleMaterialPerPart = true,
            MaxTextureSize = 0 // uncapped: density must be preserved 1:1
        };

        mesh.WriteObj(Path.Combine(testPath, "out.obj"));

        using var atlas = Image.Load<Rgba32>(Path.Combine(testPath, mesh.Materials[0].Texture!));
        int edge = atlas.Width;

        // Inside the drawn chart (drawn pixels are opaque, the untouched atlas background is not):
        int pure = 0, grayish = 0;
        atlas.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++)
                {
                    var p = row[x];
                    if (p.A < 128) continue;                                                 // untouched background
                    if (Math.Abs((int)p.R - p.G) > 8 || Math.Abs((int)p.G - p.B) > 8) continue; // ignore tint
                    int lum = p.R;
                    if (lum is < 40 or > 215) pure++;
                    else if (lum is > 80 and < 175) grayish++;
                }
            }
        });
        pure.ShouldBeGreaterThan(0, "atlas must contain the stripe extremes");
        grayish.ShouldBeLessThan(pure / 10,
            "the vast majority of inlier pixels must be pure stripes; grayish pixels indicate a downscaled U axis");
    }

    [Test]
    public void WriteObj_SingleMaterialPerPart_TooManyChartsForCap_Throws()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_SingleMaterialPerPart_TooManyChartsForCap_Throws));
        var texturePath = Path.Combine(testPath, "source.png");
        using (var texture = new Image<Rgba32>(256, 256, new Rgba32(255, 0, 0, 255)))
            texture.SaveAsPng(texturePath);

        const int chartCount = 64;
        var vertices = new[] { new Vertex3(0, 0, 0), new Vertex3(1, 0, 0), new Vertex3(0, 1, 0) };
        var textureVertices = new List<Vertex2>(chartCount * 3);
        var faces = new List<FaceT>(chartCount);
        for (var i = 0; i < chartCount; i++)
        {
            var textureIndex = textureVertices.Count;
            var u = i * 0.015;
            textureVertices.Add(new Vertex2(u, 0));
            textureVertices.Add(new Vertex2(u + 0.01, 0));
            textureVertices.Add(new Vertex2(u, 0.01));
            faces.Add(new FaceT(0, 1, 2, textureIndex, textureIndex + 1, textureIndex + 2, 0));
        }

        var mesh = new MeshT(vertices, textureVertices, faces, [new Materials.Material("tiny", texturePath)])
        {
            TexturesStrategy = TexturesStrategy.Repack,
            SingleMaterialPerPart = true,
            MaxTextureSize = 4 // cannot ever hold 64 distinct charts
        };

        var ex = Should.Throw<InvalidOperationException>(
            () => mesh.WriteObj(Path.Combine(testPath, "mesh.obj")));
        ex.Message.ShouldContain("--max-texture-size");
    }

    [Test]
    public void WriteObj_SingleMaterialPerPart_PngSources_WritePngAtlas()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_SingleMaterialPerPart_PngSources_WritePngAtlas));
        var texturePath = Path.Combine(testPath, "source.png");
        using (var texture = new Image<Rgba32>(16, 16, new Rgba32(10, 200, 30, 255)))
            texture.SaveAsPng(texturePath);

        var mesh = new MeshT(
            [new Vertex3(0, 0, 0), new Vertex3(1, 0, 0), new Vertex3(0, 1, 0)],
            [new Vertex2(0, 0), new Vertex2(1, 0), new Vertex2(0, 1)],
            [new FaceT(0, 1, 2, 0, 1, 2, 0)],
            [new Materials.Material("pngmat", texturePath)])
        {
            TexturesStrategy = TexturesStrategy.Repack,
            SingleMaterialPerPart = true
        };

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));
        Path.GetExtension(mesh.Materials[0].Texture!).ShouldBe(".png");
    }

    [Test]
    public void WriteObj_SingleMaterialPerPart_JpegSources_WriteJpegAtlas()
    {
        var testPath = GetTestOutputPath(nameof(WriteObj_SingleMaterialPerPart_JpegSources_WriteJpegAtlas));
        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube", "cube.obj"));
        mesh.TexturesStrategy = TexturesStrategy.Repack;
        mesh.SingleMaterialPerPart = true;

        mesh.WriteObj(Path.Combine(testPath, "mesh.obj"));

        Path.GetExtension(mesh.Materials[0].Texture!).ShouldBe(".jpg");
        File.Exists(Path.Combine(testPath, mesh.Materials[0].Texture!)).ShouldBeTrue();
    }

    [Test]
    public void WriteObj_Brighton_Repacking()
    {
        using var fs = new TestFS(BrightonTexturingTestUrl, nameof(Mesh3Tests));

        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(fs.TestFolder, "odm_textured_model_geo.obj"));

        mesh.TexturesStrategy = TexturesStrategy.Repack;
        var outputPath = Path.Combine(fs.TestFolder, "output");
        Directory.CreateDirectory(outputPath);

        mesh.WriteObj(Path.Combine(outputPath, "mesh.obj"));
    }

    [Test]
    public void WriteObj_Brighton_SingleMaterialPerPart_ProducesReloadableSingleMaterialAtlas()
    {
        using var fs = new TestFS(BrightonTexturingTestUrl, nameof(WriteObj_Brighton_SingleMaterialPerPart_ProducesReloadableSingleMaterialAtlas));
        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(fs.TestFolder, "odm_textured_model_geo.obj"));
        mesh.TexturesStrategy = TexturesStrategy.Repack;
        mesh.SingleMaterialPerPart = true;

        var outputPath = Path.Combine(fs.TestFolder, "single-material.obj");
        mesh.WriteObj(outputPath);

        var output = (MeshT)MeshUtils.LoadMesh(outputPath);
        output.Materials.Count.ShouldBe(1);
        output.Faces.All(face => face.MaterialIndex == 0).ShouldBeTrue();
        File.Exists(Path.Combine(fs.TestFolder, output.Materials[0].Texture!)).ShouldBeTrue();
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

    [Test]
    public void Orientation_TestOk()
    {
        var v1 = new Vertex3(0, 0, 0);
        var v2 = new Vertex3(1, 0, 0);
        var v3 = new Vertex3(0, 1, 0);
        
        var o = Common.Orientation(v1, v2, v3);

        o.Z.ShouldBe(1);
        o.X.ShouldBe(0);
        o.Y.ShouldBe(0);
    }
    
    
    [Test]
    public void Orientation_TestZero()
    {
        var v1 = new Vertex3(0, 0, 0);
        var v2 = new Vertex3(0, 0, 0);
        var v3 = new Vertex3(0, 0, 0);
        
        var o = Common.Orientation(v1, v2, v3);

        o.Z.ShouldBe(0);
        o.X.ShouldBe(0);
        o.Y.ShouldBe(0);
    }

    [Test]
    public void Orientation_TestCubeMesh()
    {
        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube2/cube.obj"));

        var orientation = mesh.GetAverageOrientation();

        orientation.X.ShouldBe(0);
        orientation.Y.ShouldBe(0);
        orientation.Z.ShouldBe(0);
    }
    
    [Test]
    public void Orientation_TestBrighton()
    {
        
        using var fs = new TestFS(BrightonTexturingTestUrl, nameof(Mesh3Tests));

        var mesh = (MeshT)MeshUtils.LoadMesh(Path.Combine(fs.TestFolder, "odm_textured_model_geo.obj"));

        var orientation = mesh.GetAverageOrientation();

    }
}