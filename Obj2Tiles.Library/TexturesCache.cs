using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Obj2Tiles.Library;

public static class TexturesCache
{
    private static readonly ConcurrentDictionary<string, Image<Rgba32>> _textures = new();
    
    public static Image<Rgba32> GetTexture(string textureName)
    {
        if (!_textures.ContainsKey(textureName))
        {
            var texture = Image.Load<Rgba32>(textureName);
            
            _textures.TryAdd(textureName, texture);
            
            return texture;
            
        }
        
        return _textures[textureName];
    }
    
    public static void Clear()
    {
        foreach(var texture in _textures)
        {
            texture.Value.Dispose();
        }
        _textures.Clear();
    }
}