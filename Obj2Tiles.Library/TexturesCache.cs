using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Obj2Tiles.Library;

public static class TexturesCache
{
    private static readonly ConcurrentDictionary<string, Image<Rgba32>> Textures = new();
    
    public static Image<Rgba32> GetTexture(string textureName)
    {
        if (Textures.TryGetValue(textureName, out var txout))
            return txout;

        var texture = Image.Load<Rgba32>(textureName);
        Textures.TryAdd(textureName, texture);

        return texture;

    }
    
    public static void Clear()
    {
        foreach(var texture in Textures)
        {
            texture.Value.Dispose();
        }
        Textures.Clear();
    }
}