using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Obj2Tiles.Common;
using Obj2Tiles.Library.Textures;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Obj2Tiles.Library.Test;

/// <summary>
/// The colour textures whose samples are wider than 8 bits (the reflectance of multispectral and
/// thermal models) are brought to 8 bits by a linear stretch from the 2nd to the 98th percentile of
/// their non-zero samples, one stretch for all the textures of a model; 8 bit textures are read as
/// ImageSharp reads them.
/// </summary>
public class TextureToneMapTests
{
    private string _folder = null!;

    [SetUp]
    public void SetUp()
    {
        // A new folder for every test: the tone maps of the textures are kept by their path.
        _folder = Path.Combine(Path.GetTempPath(), "Obj2Tiles.ToneMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown()
    {
        TexturesCache.Clear();
        Directory.Delete(_folder, true);
    }

    #region SampleHistogram and ToneMap

    [Test]
    public void SampleHistogram_ZeroAndValuesThatAreNotFinite_AreNotCounted()
    {
        var samples = new SampleHistogram();
        samples.Add(0.0f);
        samples.Add(-0.0f);
        samples.Add(float.NaN);
        samples.Add(float.PositiveInfinity);
        samples.Add(float.NegativeInfinity);
        samples.Count.ShouldBe(0UL);
        samples.Quantile(1, 2).ShouldBe(0.0f);

        samples.Add(7.0f);
        samples.Count.ShouldBe(1UL);
    }

    [TestCase(1.0f)]
    [TestCase(1.0f / 65535.0f)] // float reflectance as well as 16 bit
    public void SampleHistogram_AQuantile_IsWithinOnePercentOfTheSampleItStandsFor(float scale)
    {
        var samples = new SampleHistogram();
        for (var value = 1; value <= 1000; value++)
            samples.Add(value * 40 * scale);

        samples.Quantile(1, 2).ShouldBe(20000.0f * scale, 20000.0f * scale * 0.01f);
        samples.Quantile(2, 100).ShouldBeLessThanOrEqualTo(samples.Quantile(98, 100));
    }

    [Test]
    public void SampleHistogram_NegativeValues_SortBeforePositiveOnes()
    {
        var samples = new SampleHistogram();
        for (var i = 0; i < 50; i++)
        {
            samples.Add(-0.5f);
            samples.Add(-0.001f);
            samples.Add(0.25f);
            samples.Add(4.0f);
        }

        samples.Quantile(1, 4).ShouldBe(-0.5f, 0.005f);
        samples.Quantile(2, 4).ShouldBe(-0.001f, 0.00001f);
        samples.Quantile(3, 4).ShouldBe(0.25f, 0.0025f);
        samples.Quantile(4, 4).ShouldBe(4.0f, 0.04f);
    }

    [Test]
    public void SampleHistogram_Histograms_AddUpAndKeepTheLargestFullScale()
    {
        var a = new SampleHistogram();
        var b = new SampleHistogram();
        a.Add(100.0f);
        a.AddFullScale(1.0f);
        b.Add(200.0f);
        b.Add(300.0f);
        b.AddFullScale(65535.0f);

        a.Add(b);

        a.Count.ShouldBe(3UL);
        a.FullScale.ShouldBe(65535.0f);
    }

    [Test]
    public void ToneMap_TheLowValueAndBelowAre0_AndTheHighOneAndAbove255()
    {
        var map = new ToneMap(1000, 3000);
        map.Map(0).ShouldBe((byte)0);
        map.Map(1000).ShouldBe((byte)0);
        map.Map(2000).ShouldBe((byte)128); // 127.5, rounded
        map.Map(3000).ShouldBe((byte)255);
        map.Map(60000).ShouldBe((byte)255);
        map.Map(double.NaN).ShouldBe((byte)0);
    }

    [Test]
    public void ToneMap_TheStretch_GoesFromThe2ndToThe98thPercentile()
    {
        // 6000 to 38000: where the 16 bit reflectance of a multispectral camera lies, in a small part of
        // the range of the type; scaled by the range of the type it would be 23 to 148
        var samples = new SampleHistogram();
        for (var value = 6000; value < 38000; value += 4)
            samples.Add(value);

        var map = ToneMap.Stretch(samples);

        map.Low.ShouldBe(6640, 6640 * 0.01);
        map.High.ShouldBe(37360, 37360 * 0.01);
        map.Map(6000).ShouldBe((byte)0);
        map.Map(38000).ShouldBe((byte)255);
        ((int)map.Map(22000)).ShouldBeInRange(125, 131);
    }

    [Test]
    public void ToneMap_AFlatImage_IsStretchedFromZeroToTheFullScaleOfItsType()
    {
        var integers = new SampleHistogram();
        integers.AddFullScale(65535.0f);
        for (var i = 0; i < 100; i++)
            integers.Add(0x7B40);
        ToneMap.Stretch(integers).Map(0x7B40).ShouldBe((byte)123); // 0x7B40 / 65535 * 255 = 122.6

        var floats = new SampleHistogram();
        floats.AddFullScale(1.0f);
        for (var i = 0; i < 100; i++)
            floats.Add(0.5f);
        ToneMap.Stretch(floats).Map(0.5).ShouldBe((byte)128);

        // nothing counted: the zeros stay black
        var none = new SampleHistogram();
        none.AddFullScale(65535.0f);
        ToneMap.Stretch(none).Map(0).ShouldBe((byte)0);
    }

    #endregion

    #region Textures

    // A ramp of reflectance from 6000 to 38000 over 64 x 4 pixels, with a column of zeros (the texels no
    // photo covers) on the left.
    private static double Ramp16(int x, int y, int s) => x == 0 ? 0 : 6000 + (x - 1) * 500 + y * 10;

    private static double RampFloat(int x, int y, int s) => x == 0 ? 0 : 0.01 + (x - 1) * 0.003 + y * 0.0001;

    // The stretch the samples of fn over width x height must get, worked out apart from the reader.
    private static ToneMap ExpectedStretch(int width, int height, float fullScale, Func<int, int, int, double> fn)
    {
        var samples = new SampleHistogram();
        samples.AddFullScale(fullScale);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            samples.Add((float)fn(x, y, 0));
        return ToneMap.Stretch(samples);
    }

    private string Tiff(string name, int samplesPerPixel, int bits, bool floatingPoint, ushort photometric,
        Func<int, int, int, double> sample, int width = 64, int height = 4, bool deflate = true)
    {
        var path = Path.Combine(_folder, name);
        TestTiff.Write(path, width, height, samplesPerPixel, bits, floatingPoint, photometric, sample, deflate);
        return path;
    }

    private static void ShouldBeGray(Rgba32 pixel, byte level)
    {
        pixel.ShouldBe(new Rgba32(level, level, level, 255));
    }

    [Test]
    public void OneBandOf16BitSamplesTaggedRgb_AsOpenDroneMapWritesIt_IsReadAndStretched()
    {
        // deflate, one strip without RowsPerStrip, one sample per pixel, Photometric RGB
        var path = Tiff("nir.tif", 1, 16, false, TestTiff.Rgb, Ramp16);
        Should.Throw<Exception>(() => Image.Load<Rgba32>(path)); // what ImageSharp makes of it by itself

        var expected = ExpectedStretch(64, 4, 65535, Ramp16);
        var texture = TexturesCache.GetTexture(path);

        texture.Width.ShouldBe(64);
        texture.Height.ShouldBe(4);
        for (var y = 0; y < 4; y++)
        for (var x = 0; x < 64; x++)
            ShouldBeGray(texture[x, y], expected.Map(Ramp16(x, y, 0)));

        // the zeros stay black and the stretch spans the whole range of 8 bits
        ShouldBeGray(texture[0, 0], 0);
        ShouldBeGray(texture[1, 0], 0);
        ShouldBeGray(texture[63, 3], 255);
        texture[32, 2].R.ShouldBeInRange((byte)100, (byte)155);
    }

    [Test]
    public void OneBandOfFloatReflectance_IsReadAndStretched()
    {
        var path = Tiff("red.tif", 1, 32, true, TestTiff.Rgb, RampFloat);

        var expected = ExpectedStretch(64, 4, 1, RampFloat);
        var texture = TexturesCache.GetTexture(path);

        for (var y = 0; y < 4; y++)
        for (var x = 0; x < 64; x++)
            ShouldBeGray(texture[x, y], expected.Map(RampFloat(x, y, 0)));
        ShouldBeGray(texture[0, 0], 0);
        ShouldBeGray(texture[63, 3], 255);
    }

    [Test]
    public void GrayOf16BitSamples_IsStretched_AndMinIsWhiteIsInverted()
    {
        var expected = ExpectedStretch(64, 4, 65535, Ramp16);
        var black = TexturesCache.GetTexture(Tiff("black.tif", 1, 16, false, TestTiff.MinIsBlack, Ramp16,
            deflate: false));
        var white = TexturesCache.GetTexture(Tiff("white.tif", 1, 16, false, TestTiff.MinIsWhite, Ramp16));

        for (var x = 0; x < 64; x++)
        {
            ShouldBeGray(black[x, 1], expected.Map(Ramp16(x, 1, 0)));
            ShouldBeGray(white[x, 1], (byte)(255 - expected.Map(Ramp16(x, 1, 0))));
        }
    }

    [Test]
    public void RgbOf16BitSamples_ItsChannelsShareOneStretch()
    {
        // the same values in every channel, in another order: a value is the same level in each
        double Sample(int x, int y, int s) => Ramp16((x + 20 * s) % 64, y, s);
        var path = Tiff("rgb.tif", 3, 16, false, TestTiff.Rgb, Sample);

        var samples = new SampleHistogram();
        samples.AddFullScale(65535);
        for (var y = 0; y < 4; y++)
        for (var x = 0; x < 64; x++)
        for (var s = 0; s < 3; s++)
            samples.Add((float)Sample(x, y, s));
        var expected = ToneMap.Stretch(samples);

        var texture = TexturesCache.GetTexture(path);
        for (var x = 0; x < 64; x++)
        {
            var pixel = texture[x, 2];
            pixel.R.ShouldBe(expected.Map(Sample(x, 2, 0)));
            pixel.G.ShouldBe(expected.Map(Sample(x, 2, 1)));
            pixel.B.ShouldBe(expected.Map(Sample(x, 2, 2)));
            pixel.A.ShouldBe((byte)255);
        }
    }

    [Test]
    public void TheTexturesOfAModel_ShareOneStretch()
    {
        // Two textures of flat halves: the first 10000 and 30000, the second 30000 and 50000. Stretched
        // each on its own, 30000 is white in the first and black in the second; stretched together it is
        // the same gray in both, as a surface across two textures of an atlas must be.
        string Halves(string name, double left, double right) =>
            Tiff(name, 1, 16, false, TestTiff.Rgb, (x, _, _) => x < 8 ? left : right, 16, 16);

        var first = Halves("first.tif", 10000, 30000);
        var second = Halves("second.tif", 30000, 50000);
        TexturesCache.AddModel([first, second]);

        var a = TexturesCache.GetTexture(first);
        var b = TexturesCache.GetTexture(second);
        for (var y = 0; y < 16; y++)
        {
            a[0, y].R.ShouldBe((byte)0);
            b[15, y].R.ShouldBe((byte)255);
            a[15, y].ShouldBe(b[0, y], $"30000 at row {y}");
            ((int)a[15, y].R).ShouldBeInRange(124, 132);
        }

        // read without its model, a texture is stretched on its own
        var alone = TexturesCache.GetTexture(Halves("alone.tif", 10000, 30000));
        alone[15, 0].R.ShouldBe((byte)255);
    }

    [Test]
    public void TheTexturesOfAModel_KeepTheirStretch_WhenAPartOfTheModelIsLoadedAgain()
    {
        // The meshes of the split and the root tile reference some of the textures of the model only:
        // they must not get a stretch of their own.
        var first = Tiff("first.tif", 1, 16, false, TestTiff.Rgb, (x, _, _) => x < 8 ? 10000 : 30000, 16, 16);
        var second = Tiff("second.tif", 1, 16, false, TestTiff.Rgb, (x, _, _) => x < 8 ? 30000 : 50000, 16, 16);
        TexturesCache.AddModel([first, second]);
        TexturesCache.AddModel([second]);

        ((int)TexturesCache.GetTexture(second)[0, 0].R).ShouldBeInRange(124, 132);
        TexturesCache.Clear();
        ((int)TexturesCache.GetTexture(second)[0, 0].R).ShouldBeInRange(124, 132);
    }

    [Test]
    public void LoadingAMesh_MakesTheTexturesOfItsMaterialsShareOneStretch()
    {
        Tiff("first.tif", 1, 16, false, TestTiff.Rgb, (x, _, _) => x < 8 ? 10000 : 30000, 16, 16);
        var second = Tiff("second.tif", 1, 16, false, TestTiff.Rgb, (x, _, _) => x < 8 ? 30000 : 50000, 16, 16);
        File.WriteAllText(Path.Combine(_folder, "model.mtl"),
            "newmtl first\nmap_Kd first.tif\n\nnewmtl second\nmap_Kd second.tif\n");
        var obj = Path.Combine(_folder, "model.obj");
        File.WriteAllText(obj, "mtllib model.mtl\n" +
                               "v 0 0 0\nv 1 0 0\nv 0 1 0\nv 1 1 0\n" +
                               "vt 0 0\nvt 1 0\nvt 0 1\nvt 1 1\n" +
                               "usemtl first\nf 1/1 2/2 3/3\n" +
                               "usemtl second\nf 2/2 4/4 3/3\n");

        Geometry.MeshUtils.LoadMesh(obj);

        // 30000 is the middle of the stretch of the model, not the low end of the second texture
        ((int)TexturesCache.GetTexture(second)[0, 0].R).ShouldBeInRange(124, 132);
    }

    [Test]
    public void AFlatTexture_IsStretchedFromZeroToTheFullScaleOfItsType()
    {
        var integers = TexturesCache.GetTexture(Tiff("flat16.tif", 1, 16, false, TestTiff.Rgb, (_, _, _) => 0x7B40));
        ShouldBeGray(integers[3, 2], 123);

        var floats = TexturesCache.GetTexture(Tiff("flat32.tif", 1, 32, true, TestTiff.Rgb, (_, _, _) => 0.5));
        ShouldBeGray(floats[3, 2], 128);

        var zeros = TexturesCache.GetTexture(Tiff("zeros.tif", 1, 16, false, TestTiff.Rgb, (_, _, _) => 0));
        ShouldBeGray(zeros[3, 2], 0);
    }

    [Test]
    public void OneBandOf8BitSamplesTaggedRgb_IsReadAsItIs()
    {
        static double Sample(int x, int y, int s) => x * 4 + y;
        var path = Tiff("gray8.tif", 1, 8, false, TestTiff.Rgb, Sample);
        Should.Throw<Exception>(() => Image.Load<Rgba32>(path)); // what ImageSharp makes of it by itself

        var texture = TexturesCache.GetTexture(path);
        for (var x = 0; x < 64; x++)
            ShouldBeGray(texture[x, 3], (byte)Sample(x, 3, 0));
    }

    [Test]
    public void EightBitTextures_AreReadAsImageSharpReadsThem()
    {
        var tiff = Tiff("rgb8.tif", 3, 8, false, TestTiff.Rgb, (x, y, s) => (x * 3 + y * 7 + s * 50) % 256);
        var png = Path.Combine(_folder, "rgb8.png");
        using (var source = Image.Load<Rgba32>(tiff))
            source.SaveAsPng(png);

        foreach (var path in new[] { tiff, png })
        {
            using var reference = Image.Load<Rgba32>(path);
            var texture = TexturesCache.GetTexture(path);
            for (var y = 0; y < reference.Height; y++)
            for (var x = 0; x < reference.Width; x++)
                texture[x, y].ShouldBe(reference[x, y]);

            // the copy for the compression of the textures keeps the pixel format of the file
            using var copy = TexturesCache.ReadTexture(path);
            using var file = Image.Load(path);
            copy.PixelType.BitsPerPixel.ShouldBe(file.PixelType.BitsPerPixel);
        }
    }

    [Test]
    public void ACopyOfA16BitTexture_IsStretchedAsTheSharedOne()
    {
        var path = Tiff("copy.tif", 1, 16, false, TestTiff.Rgb, Ramp16);

        using var copy = TexturesCache.ReadTexture(path);
        var shared = TexturesCache.GetTexture(path);

        var pixels = copy.CloneAs<Rgba32>();
        for (var x = 0; x < 64; x++)
            pixels[x, 1].ShouldBe(shared[x, 1]);
    }

    [Test]
    public void ANormalMapOf16BitSamples_IsNotToneMapped()
    {
        // a normal map holds directions: its samples are scaled by the range of the type, as ImageSharp does
        var path = Tiff("normal.tif", 1, 16, false, TestTiff.MinIsBlack, Ramp16);
        using var reference = Image.Load<Rgba32>(path);

        var normal = TexturesCache.GetNormalMap(path);
        for (var x = 0; x < 64; x++)
            normal[x, 1].ShouldBe(reference[x, 1]);
    }

    [Test]
    public void ATruncated16BitTexture_FailsWithItsPath()
    {
        var path = Tiff("truncated.tif", 1, 16, false, TestTiff.Rgb, Ramp16, deflate: false);
        // Drop the second half of the strip; the directory, which follows the strip, still declares all of
        // it, so the strip runs past the end of the file.
        var bytes = File.ReadAllBytes(path);
        var directory = BitConverter.ToInt32(bytes, 4);
        var kept = 8 + (directory - 8) / 2;
        var damaged = bytes.Take(kept).Concat(bytes.Skip(directory)).ToArray();
        BitConverter.GetBytes(kept).CopyTo(damaged, 4);
        File.WriteAllBytes(path, damaged);

        var error = Should.Throw<InvalidDataException>(() => TexturesCache.GetTexture(path));
        error.Message.ShouldContain(path);
    }

    #endregion
}
