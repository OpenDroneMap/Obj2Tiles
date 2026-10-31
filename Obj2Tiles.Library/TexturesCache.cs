using System.Collections.Concurrent;
using Obj2Tiles.Library.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Obj2Tiles.Library;

public static class TexturesCache
{
    private static readonly ConcurrentDictionary<string, Lazy<Image<Rgba32>>> Textures = new();
    private static readonly ConcurrentDictionary<string, Lazy<Image<Rgba32>>> NormalMaps = new();

    // The tone map of each colour texture, by its full path. They outlive Clear(): the textures of a
    // model are read again after it (the root tile of the tileset) and must look the same.
    private static readonly ConcurrentDictionary<string, Lazy<ToneMap>> ToneMaps = new();
    private static readonly object ModelLock = new();

    /// <summary>
    /// Makes the colour textures of one model share one tone map: the samples wider than 8 bits of all of
    /// them are measured together, the first time one of them is read, so the textures of the model keep
    /// one brightness from one atlas to the next. Textures that already have a tone map keep it.
    /// </summary>
    public static void AddModel(IEnumerable<string> colourTextures)
    {
        var paths = colourTextures.Select(Path.GetFullPath).Distinct().ToArray();
        lock (ModelLock)
        {
            var shared = new Lazy<ToneMap>(() => MeasureToneMap(paths));
            foreach (var path in paths)
                ToneMaps.TryAdd(path, shared);
        }
    }

    /// <summary>The colour texture at <paramref name="path"/>, read once and shared.</summary>
    public static Image<Rgba32> GetTexture(string path) =>
        Textures.GetOrAdd(path, p => new Lazy<Image<Rgba32>>(() =>
            TiffSamples.TryRead(p, () => ToneMapOf(p)) ?? Image.Load<Rgba32>(p))).Value;

    /// <summary>
    /// A new copy of the colour texture at <paramref name="path"/>, for the caller to dispose: in the pixel
    /// format of the file, or 8 bit RGBA when its samples are brought to 8 bits here. With
    /// <paramref name="sizeOf"/> it has the size that function gives for the size of the file (null: the
    /// size of the file); see <see cref="LoadAtSize"/>.
    /// </summary>
    public static Image ReadTexture(string path, Func<Size, Size?>? sizeOf = null) =>
        TiffSamples.TryRead(path, () => ToneMapOf(path)) is { } samples
            ? ResizeTo(samples, sizeOf?.Invoke(samples.Size))
            : LoadAtSize(path, sizeOf);

    /// <summary>
    /// The image at <paramref name="path"/>, for the caller to dispose, at the size <paramref name="sizeOf"/>
    /// gives for the size of the file (null: the size of the file). A smaller image is decoded at that size
    /// (a JPEG at a reduced scale of its blocks) rather than decoded whole and then resized: an 8192 px
    /// texture brought down to a 256 px one is never held at full size.
    /// </summary>
    public static Image LoadAtSize(string path, Func<Size, Size?>? sizeOf = null)
    {
        var target = sizeOf?.Invoke(Image.Identify(path).Size);
        if (target is not { } size)
            return Image.Load(path);

        // The decoder fits the image inside the size, keeping its aspect; ResizeTo makes it exact.
        return ResizeTo(Image.Load(new DecoderOptions { TargetSize = size }, path), size);
    }

    private static Image ResizeTo(Image image, Size? size)
    {
        if (size is { } s && image.Size != s)
            image.Mutate(x => x.Resize(s.Width, s.Height));
        return image;
    }

    /// <summary>
    /// The normal map at <paramref name="path"/>, read once and shared. Its samples hold directions, not
    /// brightness, so they are never tone mapped.
    /// </summary>
    public static Image<Rgba32> GetNormalMap(string path) =>
        NormalMaps.GetOrAdd(path, p => new Lazy<Image<Rgba32>>(() => Image.Load<Rgba32>(p))).Value;

    /// <summary>Disposes the textures read so far; the tone maps of the models are kept.</summary>
    public static void Clear()
    {
        foreach (var cache in new[] { Textures, NormalMaps })
        {
            foreach (var lazy in cache.Values)
                if (lazy.IsValueCreated)
                    lazy.Value.Dispose();
            cache.Clear();
        }
    }

    // A texture read without its model (AddModel) is a model of its own.
    private static ToneMap ToneMapOf(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!ToneMaps.ContainsKey(fullPath))
            AddModel([fullPath]);
        return ToneMaps[fullPath].Value;
    }

    private static ToneMap MeasureToneMap(IEnumerable<string> paths)
    {
        var samples = new SampleHistogram();
        var measured = 0;
        foreach (var path in paths)
            if (File.Exists(path) && TiffSamples.TryAddSamples(path, samples))
                measured++;

        var toneMap = ToneMap.Stretch(samples);
        if (measured > 0)
            Console.WriteLine($" ?> Tone map of {measured} texture(s) with samples wider than 8 bits: {toneMap}");
        return toneMap;
    }
}
