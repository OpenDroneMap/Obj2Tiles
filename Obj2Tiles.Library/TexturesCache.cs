using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Obj2Tiles.Library;

public static class TexturesCache
{
    private static readonly ConcurrentDictionary<string, Lazy<Image<Rgba32>>> Textures = new();

    public static Image<Rgba32> GetTexture(string textureName)
    {
        return Textures.GetOrAdd(textureName, p => new Lazy<Image<Rgba32>>(() => Image.Load<Rgba32>(p))).Value;
    }

    public static void Clear()
    {
        foreach (var lazy in Textures.Values)
            if (lazy.IsValueCreated)
                lazy.Value.Dispose();
        Textures.Clear();
    }
}