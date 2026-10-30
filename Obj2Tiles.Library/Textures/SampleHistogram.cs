namespace Obj2Tiles.Library.Textures;

/// <summary>
/// The distribution of the samples of one or more textures whose samples are wider than 8 bits
/// (16 bit integers, 32 bit floats). The textures of a multispectral or thermal model hold reflectance
/// or radiance, which fills a small part of the range of the type, so they cannot be scaled to 8 bits
/// by the range of the type alone (a reflectance of 0.05 in a float, or 15000 in 16 bits, is black or
/// near black).
/// </summary>
/// <remarks>
/// A sample is counted in the bin of the 16 high bits of its 32 bit float (the sign, the exponent and
/// 7 bits of the mantissa), so the bins have the same relative width whatever the range and the scale
/// of the values: a quantile is within 1 % of the sample it stands for. Zero is not counted: it is the
/// texel no photo covers in the texture atlases of photogrammetry, and counting it would put the low
/// quantile there. Values that are not finite are not counted either.
/// </remarks>
public sealed class SampleHistogram
{
    private const int Bins = 65536;

    private readonly ulong[] _bins = new ulong[Bins];

    /// <summary>The number of samples counted.</summary>
    public ulong Count { get; private set; }

    /// <summary>
    /// The value the samples of the type of a texture span (65535 for 16 bit integers, 1 for floats,
    /// which hold a fraction): the range the samples are scaled from when they are all the same value
    /// or when none is counted. The largest one added is kept.
    /// </summary>
    public float FullScale { get; private set; }

    public void Add(float value)
    {
        if (value == 0 || !float.IsFinite(value))
            return;
        _bins[BinOf(value)]++;
        Count++;
    }

    /// <summary>Adds the samples of <paramref name="other"/>.</summary>
    public void Add(SampleHistogram other)
    {
        for (var i = 0; i < Bins; i++)
            _bins[i] += other._bins[i];
        Count += other.Count;
        AddFullScale(other.FullScale);
    }

    public void AddFullScale(float fullScale)
    {
        if (fullScale > FullScale)
            FullScale = fullScale;
    }

    /// <summary>
    /// The value of the first bin (its 16 high bits) where the samples counted up to it reach
    /// <paramref name="numerator"/> / <paramref name="denominator"/> of the count; 0 when none is counted.
    /// </summary>
    public float Quantile(ulong numerator, ulong denominator)
    {
        if (Count == 0 || denominator == 0)
            return 0;

        // In integers, so the bin is the same on every platform: the rank is count * numerator /
        // denominator rounded down, worked out without the product, which could overflow.
        var rank = Count / denominator * numerator + Count % denominator * numerator / denominator;
        ulong counted = 0;
        for (var i = 0; i < Bins; i++)
        {
            counted += _bins[i];
            if (counted != 0 && counted >= rank)
                return ValueOf((ushort)i);
        }

        return ValueOf(Bins - 1);
    }

    // The 16 high bits of the float, made to sort as the values do: a positive value has the sign bit
    // set and keeps the rest, a negative one has every bit flipped, so the larger its magnitude the
    // lower its bin.
    private static ushort BinOf(float value)
    {
        var high = (ushort)(BitConverter.SingleToUInt32Bits(value) >> 16);
        return (high & 0x8000) != 0 ? (ushort)~high : (ushort)(high | 0x8000);
    }

    private static float ValueOf(ushort bin)
    {
        var high = (bin & 0x8000) != 0 ? (ushort)(bin & 0x7fff) : (ushort)~bin;
        return BitConverter.UInt32BitsToSingle((uint)high << 16);
    }
}
