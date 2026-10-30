namespace Obj2Tiles.Library.Textures;

/// <summary>
/// Maps a sample wider than 8 bits to 8 bits with a linear stretch between two values: the low one and
/// below are 0, the high one and above 255. Not a number is 0.
/// </summary>
public sealed class ToneMap
{
    private readonly double _scale;

    /// <summary>0 to 65535, the range of 16 bit samples.</summary>
    public static ToneMap FullRange { get; } = new(0, 65535);

    public ToneMap(double low, double high)
    {
        Low = low;
        High = high > low ? high : low + 1;
        _scale = 255.0 / (High - Low);
    }

    public double Low { get; }
    public double High { get; }

    /// <summary>
    /// The stretch of the map view of DroneDB and of most GIS programs: from the 2nd to the 98th
    /// percentile of the samples counted, so a few saturated or dark texels do not take the contrast of
    /// the rest. When the two percentiles are the same value (a texture of one flat value, or of zeros
    /// only) the stretch is from 0 to the full scale of the samples.
    /// </summary>
    public static ToneMap Stretch(SampleHistogram samples)
    {
        double low = samples.Quantile(2, 100);
        double high = samples.Quantile(98, 100);
        if (high > low)
            return new ToneMap(low, high);
        return new ToneMap(0, samples.FullScale > 0 ? samples.FullScale : 65535);
    }

    public byte Map(double value)
    {
        if (!(value > Low))
            return 0;
        if (value >= High)
            return 255;
        return (byte)(int)((value - Low) * _scale + 0.5);
    }

    public override string ToString() => $"{Low:G6} -> {High:G6}";
}
