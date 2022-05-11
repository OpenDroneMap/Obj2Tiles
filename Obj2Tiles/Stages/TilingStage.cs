using System.Diagnostics;
using System.Text;
using Arctron.Obj23dTiles;
using Arctron.Obj2Gltf;

namespace Obj2Tiles.Stages;

public static partial class StagesFacade
{

    public static async Task Tile(string sourcePath, string destPath, int lods)
    {

        for (var lod = 0; lod < lods; lod++)
        {
            var files = Directory.GetFiles(Path.Combine(sourcePath, "LOD-" + lod), "*.obj");

            foreach (var file in files)
            {
                
                var outputFolder = Path.Combine(destPath, "LOD-" + lod);
                Directory.CreateDirectory(outputFolder);
                
                var outputFile = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(file), ".b3dm"));
                //var obj = TilesConverter.WriteB3dm(file, outputFile, null);
                
                //Debug.WriteLine(obj.ToString());
                
                var outputFile2 = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(file), ".gltf"));
                var opts = new GltfOptions { Binary = false, WithBatchTable = false, ObjEncoding = Encoding.UTF8};
                var converter = new Converter(file, opts);
                converter.WriteFile(outputFile2);
                
            }

        }
        

        //var converter = new TilesConverter(Path.Combine(sourcePath, "LOD-0"), destPath, new GisPosition());

        //converter.Run();


    }
}