using BitMiracle.LibTiff.Classic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace Obj2Tiles.Library.Textures;

/// <summary>
/// Reads the TIFF textures that ImageSharp cannot bring to the 8 bits of a texture by itself, by their
/// samples: samples wider than 8 bits (16 bit integers, 32 bit floats), which ImageSharp scales by the
/// range of their type (a reflectance texture comes out black), and an RGB image of fewer than three
/// samples, which ImageSharp refuses ("The number of samples in the TIFF BitsPerSample entry is not
/// supported"): the one band textures of the multispectral models of OpenDroneMap are written so. Wider
/// samples are brought to 8 bits by a <see cref="ToneMap"/>. Every other image is left to ImageSharp.
/// </summary>
internal static class TiffSamples
{
    static TiffSamples()
    {
        // LibTiff.Net prints its errors and warnings to the console by default; keep the first error of
        // the file being read, on the thread that reads it, to put it in the exception instead.
        Tiff.SetErrorHandler(new ErrorCollector());
    }

    /// <summary>
    /// The image at <paramref name="path"/>, when it is a TIFF read by its samples, with the samples wider
    /// than 8 bits mapped by the tone map <paramref name="toneMapOf"/> gives (asked for only then); null
    /// when it is another image.
    /// </summary>
    /// <exception cref="InvalidDataException">The TIFF cannot be read.</exception>
    public static Image<Rgba32>? TryRead(string path, Func<ToneMap> toneMapOf)
    {
        if (!HasTiffSignature(path))
            return null;

        using var tiff = Open(path);
        if (tiff == null || !SampleLayout.TryCreate(tiff, out var layout))
            return null;

        var image = new Image<Rgba32>(layout.Width, layout.Height, new Rgba32(0, 0, 0, 255));
        try
        {
            var pixels = new PixelSink(image, layout, layout.Wide ? toneMapOf() : ToneMap.FullRange);
            layout.ForEachSample(tiff, path, ref pixels);
        }
        catch
        {
            image.Dispose();
            throw;
        }

        return image;
    }

    /// <summary>
    /// Counts the samples of the image at <paramref name="path"/> in <paramref name="samples"/> when it is
    /// a TIFF of samples wider than 8 bits. False, with nothing counted, for any other image and for a
    /// TIFF that cannot be read (its error is reported when it is read).
    /// </summary>
    public static bool TryAddSamples(string path, SampleHistogram samples)
    {
        try
        {
            if (!HasTiffSignature(path))
                return false;

            using var tiff = Open(path);
            if (tiff == null || !SampleLayout.TryCreate(tiff, out var layout) || !layout.Wide)
                return false;

            // Counted apart, so a file that fails half way adds nothing.
            var these = new HistogramSink(new SampleHistogram());
            these.Samples.AddFullScale(layout.FullScale);
            layout.ForEachSample(tiff, path, ref these);
            samples.Add(these.Samples);
            return true;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return false;
        }
    }

    /// <summary>What is done with each sample read (a struct, so the loop over the samples is not virtual).</summary>
    private interface ISampleSink
    {
        void Add(int x, int y, int color, double value);
    }

    private readonly record struct HistogramSink(SampleHistogram Samples) : ISampleSink
    {
        public void Add(int x, int y, int color, double value) => Samples.Add((float)value);
    }

    /// <summary>Writes the samples into the pixels of an image: 8 bit samples as they are, wider ones tone mapped.</summary>
    private struct PixelSink(Image<Rgba32> image, SampleLayout layout, ToneMap toneMap) : ISampleSink
    {
        private Memory<Rgba32> _row;
        private int _rowIndex = -1;

        public void Add(int x, int y, int color, double value)
        {
            if (y != _rowIndex)
            {
                _row = image.DangerousGetPixelRowMemory(y);
                _rowIndex = y;
            }

            var level = layout.Wide ? toneMap.Map(value) : (byte)value;
            if (layout.MinIsWhite)
                level = (byte)(255 - level);

            ref var pixel = ref _row.Span[x];
            if (layout.Colors == 1)
            {
                pixel.R = pixel.G = pixel.B = level;
                return;
            }

            switch (color)
            {
                case 0: pixel.R = level; break;
                case 1: pixel.G = level; break;
                default: pixel.B = level; break;
            }
        }
    }

    private static bool HasTiffSignature(string path)
    {
        Span<byte> header = stackalloc byte[4];
        using (var stream = File.OpenRead(path))
            if (stream.ReadAtLeast(header, 4, throwOnEndOfStream: false) < 4)
                return false;

        // Classic TIFF and BigTIFF, little and big endian.
        return header is [(byte)'I', (byte)'I', 42 or 43, 0] || header is [(byte)'M', (byte)'M', 0, 42 or 43];
    }

    private static Tiff? Open(string path)
    {
        ErrorCollector.Reset();
        return Tiff.Open(path, "r");
    }

    /// <summary>How the samples of a TIFF read here are laid out.</summary>
    private sealed class SampleLayout
    {
        public int Width { get; private init; }
        public int Height { get; private init; }
        private int Bits { get; init; }
        private int SamplesPerPixel { get; init; }
        private bool FloatingPoint { get; init; }
        private bool SeparatePlanes { get; init; }
        public bool MinIsWhite { get; private init; }

        /// <summary>1 gray (the first sample), 3 RGB (the first three); the samples after them are not read.</summary>
        public int Colors { get; private init; }

        public bool Wide => Bits > 8;

        /// <summary>A float holds a fraction (a reflectance), an integer the range of its type.</summary>
        public float FullScale => FloatingPoint ? 1.0f : 65535.0f;

        private int BytesPerSample => Bits / 8;
        private int PixelBytes => SeparatePlanes ? BytesPerSample : BytesPerSample * SamplesPerPixel;

        private double Sample(byte[] buffer, int offset) => Bits switch
        {
            8 => buffer[offset],
            16 => BitConverter.ToUInt16(buffer, offset),
            _ => BitConverter.ToSingle(buffer, offset)
        };

        /// <summary>
        /// Whether the samples of <paramref name="tiff"/> are read here, and how: gray (MinIsBlack,
        /// MinIsWhite) or RGB of 16 bit integers or 32 bit floats, or RGB of fewer than three 8 bit samples.
        /// Every other TIFF (8 bit gray or RGB, palettes, YCbCr, CMYK, fewer than 8 bits) is left to ImageSharp.
        /// </summary>
        public static bool TryCreate(Tiff tiff, out SampleLayout layout)
        {
            layout = null!;
            var width = tiff.GetField(TiffTag.IMAGEWIDTH)?[0].ToInt() ?? 0;
            var height = tiff.GetField(TiffTag.IMAGELENGTH)?[0].ToInt() ?? 0;
            var photometricField = tiff.GetField(TiffTag.PHOTOMETRIC);
            if (width <= 0 || height <= 0 || photometricField == null)
                return false;

            var photometric = (Photometric)photometricField[0].ToInt();
            var bits = Defaulted(tiff, TiffTag.BITSPERSAMPLE);
            var samples = Defaulted(tiff, TiffTag.SAMPLESPERPIXEL);
            var format = (SampleFormat)Defaulted(tiff, TiffTag.SAMPLEFORMAT);
            var planar = (PlanarConfig)Defaulted(tiff, TiffTag.PLANARCONFIG);

            var rgb = photometric == Photometric.RGB;
            if (!rgb && photometric != Photometric.MINISBLACK && photometric != Photometric.MINISWHITE)
                return false;
            if (samples <= 0 || (planar != PlanarConfig.CONTIG && planar != PlanarConfig.SEPARATE))
                return false;

            var integer = format == SampleFormat.UINT && bits is 8 or 16;
            var floatingPoint = format == SampleFormat.IEEEFP && bits == 32;
            if (!integer && !floatingPoint)
                return false;
            if (bits == 8 && !(rgb && samples < 3))
                return false;

            layout = new SampleLayout
            {
                Width = width,
                Height = height,
                Bits = bits,
                SamplesPerPixel = samples,
                FloatingPoint = floatingPoint,
                SeparatePlanes = planar == PlanarConfig.SEPARATE && samples > 1,
                MinIsWhite = photometric == Photometric.MINISWHITE,
                Colors = rgb && samples >= 3 ? 3 : 1
            };
            return true;
        }

        private static int Defaulted(Tiff tiff, TiffTag tag) => tiff.GetFieldDefaulted(tag)?[0].ToInt() ?? 0;

        /// <summary>
        /// Gives <paramref name="sink"/> each colour sample of each pixel, rows from the top as they are stored
        /// (the orientation the file declares is kept, as ImageSharp keeps it), decoding the strips or the
        /// tiles of the image one at a time.
        /// </summary>
        /// <exception cref="InvalidDataException">A strip or a tile cannot be read.</exception>
        public void ForEachSample<TSink>(Tiff tiff, string path, ref TSink sink) where TSink : struct, ISampleSink
        {
            var tiled = tiff.IsTiled();
            long chunkWidth = Width, chunkHeight = 0;
            if (tiled)
            {
                var tileWidth = tiff.GetField(TiffTag.TILEWIDTH);
                var tileHeight = tiff.GetField(TiffTag.TILELENGTH);
                if (tileWidth != null && tileHeight != null)
                {
                    chunkWidth = tileWidth[0].ToUInt();
                    chunkHeight = tileHeight[0].ToUInt();
                }
                else
                    chunkWidth = 0;
            }
            else
            {
                // A file without RowsPerStrip is one strip: the default is 2^32 - 1 rows.
                chunkHeight = Math.Min(tiff.GetFieldDefaulted(TiffTag.ROWSPERSTRIP)?[0].ToUInt() ?? 0, (uint)Height);
            }

            var chunkSize = tiled ? tiff.TileSize() : tiff.StripSize();
            if (chunkWidth <= 0 || chunkHeight <= 0 || chunkSize <= 0)
                throw Unreadable(path, "the TIFF image has no strips or tiles");

            var rowBytes = (int)(chunkWidth * PixelBytes);
            var perPlane = SeparatePlanes ? 1 : Colors;
            var planes = SeparatePlanes ? Colors : 1;
            var chunk = new byte[chunkSize];

            for (var plane = 0; plane < planes; plane++)
            for (long y0 = 0; y0 < Height; y0 += chunkHeight)
            for (long x0 = 0; x0 < Width; x0 += chunkWidth)
            {
                var read = tiled
                    ? tiff.ReadEncodedTile(tiff.ComputeTile((int)x0, (int)y0, 0, (short)plane), chunk, 0, chunkSize)
                    : tiff.ReadEncodedStrip(tiff.ComputeStrip((int)y0, (short)plane), chunk, 0, chunkSize);
                var rows = (int)Math.Min(chunkHeight, Height - y0);
                var columns = (int)Math.Min(chunkWidth, Width - x0);
                if (read < 0 || read < (long)(rows - 1) * rowBytes + (long)columns * PixelBytes)
                    throw Unreadable(path, "could not read the TIFF image");

                for (var row = 0; row < rows; row++)
                {
                    var offset = row * rowBytes;
                    for (var column = 0; column < columns; column++, offset += PixelBytes)
                    for (var sample = 0; sample < perPlane; sample++)
                        sink.Add((int)x0 + column, (int)y0 + row, SeparatePlanes ? plane : sample,
                            Sample(chunk, offset + sample * BytesPerSample));
                }
            }
        }

        private static InvalidDataException Unreadable(string path, string otherwise) =>
            new($"Cannot read the TIFF texture '{path}': {ErrorCollector.LastError ?? otherwise}");
    }

    private sealed class ErrorCollector : TiffErrorHandler
    {
        [ThreadStatic] private static string? _lastError;

        public static string? LastError => _lastError;

        public static void Reset() => _lastError = null;

        public override void ErrorHandler(Tiff tif, string method, string format, params object[] args)
        {
            if (_lastError != null)
                return;
            string message;
            try
            {
                message = string.Format(format, args);
            }
            catch (FormatException)
            {
                message = format;
            }

            _lastError = string.IsNullOrEmpty(method) ? message : $"{method}: {message}";
        }

        public override void ErrorHandlerExt(Tiff tif, object clientData, string method, string format,
            params object[] args)
        {
        }

        public override void WarningHandler(Tiff tif, string method, string format, params object[] args)
        {
        }

        public override void WarningHandlerExt(Tiff tif, object clientData, string method, string format,
            params object[] args)
        {
        }
    }
}
