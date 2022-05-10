using Arctron.Obj23dTiles;

namespace Obj2Tiles.Stages;

public static partial class StagesFacade
{

    public static async Task Tile(string sourcePath, string destPath, int lods)
    {
        
        var files = Directory.GetFiles(Path.Combine(sourcePath, "LOD-0"), "*.obj");

        var converter = new TilesConverter(Path.Combine(sourcePath, "LOD-0"), destPath, new GisPosition());

        converter.Run();


    }
}