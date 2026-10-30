using System.IO.Compression;

namespace Obj2Tiles.Common;

/// <summary>
/// Writes small TIFFs for the tests of the textures, with every tag under control: the textures of the
/// multispectral models of OpenDroneMap are TIFFs that libraries write rarely (one band of 16 bit or
/// float samples tagged RGB, deflate, one strip without RowsPerStrip), so the tests make them byte by
/// byte instead of through an encoder that would normalise them. Little endian, one strip, chunky.
/// </summary>
public static class TestTiff
{
    public const ushort MinIsWhite = 0;
    public const ushort MinIsBlack = 1;
    public const ushort Rgb = 2;

    private const ushort Short = 3;
    private const ushort Long = 4;

    /// <summary>
    /// Writes a TIFF of <paramref name="width"/> x <paramref name="height"/> pixels of
    /// <paramref name="samplesPerPixel"/> samples of <paramref name="bitsPerSample"/> bits each (8 or 16
    /// bit unsigned integers, or 32 bit floats when <paramref name="floatingPoint"/>), the value of
    /// sample s of pixel (x, y) being <paramref name="sample"/>(x, y, s).
    /// </summary>
    public static void Write(string path, int width, int height, int samplesPerPixel, int bitsPerSample,
        bool floatingPoint, ushort photometric, Func<int, int, int, double> sample, bool deflate = true,
        bool writeRowsPerStrip = false)
    {
        var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            for (var s = 0; s < samplesPerPixel; s++)
            {
                var value = sample(x, y, s);
                if (floatingPoint)
                    writer.Write((float)value);
                else if (bitsPerSample == 16)
                    writer.Write((ushort)value);
                else
                    writer.Write((byte)value);
            }
        }

        var strip = deflate ? Deflate(raw.ToArray()) : raw.ToArray();

        var entries = new List<(ushort Tag, ushort Type, uint[] Values)>
        {
            (256, Long, [(uint)width]),
            (257, Long, [(uint)height]),
            (258, Short, Enumerable.Repeat((uint)bitsPerSample, samplesPerPixel).ToArray()),
            (259, Short, [deflate ? 8u : 1u]),
            (262, Short, [photometric]),
            (273, Long, [8]), // the strip follows the header
            (277, Short, [(uint)samplesPerPixel]),
            (279, Long, [(uint)strip.Length]),
            (284, Short, [1]),
            (339, Short, Enumerable.Repeat(floatingPoint ? 3u : 1u, samplesPerPixel).ToArray())
        };
        if (writeRowsPerStrip)
            entries.Add((278, Long, [(uint)height]));
        entries.Sort((a, b) => a.Tag.CompareTo(b.Tag));

        using var file = new BinaryWriter(File.Create(path));
        file.Write("II"u8);
        file.Write((ushort)42);
        file.Write(0u); // the offset of the directory, written below
        file.Write(strip);

        // The values that do not fit in the 4 bytes of their entry go before the directory.
        var outOfLine = new Dictionary<int, uint>();
        for (var i = 0; i < entries.Count; i++)
        {
            var (_, type, values) = entries[i];
            if (values.Length * (type == Short ? 2 : 4) <= 4)
                continue;
            Align(file);
            outOfLine[i] = (uint)file.BaseStream.Position;
            foreach (var value in values)
                WriteValue(file, type, value);
        }

        Align(file);
        var directory = (uint)file.BaseStream.Position;
        file.Write((ushort)entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var (tag, type, values) = entries[i];
            file.Write(tag);
            file.Write(type);
            file.Write((uint)values.Length);
            if (outOfLine.TryGetValue(i, out var offset))
            {
                file.Write(offset);
                continue;
            }

            var written = 0;
            foreach (var value in values)
            {
                WriteValue(file, type, value);
                written += type == Short ? 2 : 4;
            }

            for (; written < 4; written++)
                file.Write((byte)0);
        }

        file.Write(0u); // no other directory
        file.Seek(4, SeekOrigin.Begin);
        file.Write(directory);
    }

    private static void WriteValue(BinaryWriter writer, ushort type, uint value)
    {
        if (type == Short)
            writer.Write((ushort)value);
        else
            writer.Write(value);
    }

    private static void Align(BinaryWriter writer)
    {
        if (writer.BaseStream.Position % 2 != 0)
            writer.Write((byte)0);
    }

    private static byte[] Deflate(byte[] data)
    {
        var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data);
        return compressed.ToArray();
    }
}
