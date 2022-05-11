using System.Diagnostics;
using System.Text;
using Obj2Tiles.Tiles;
using SilentWave;
using SilentWave.Obj2Gltf;

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
                ConvertB3dm(file, outputFile);
                
                File.Copy(Path.ChangeExtension(file, ".json"), Path.ChangeExtension(outputFile, ".json"));

            }

        }
        
        // Generate tileset.json
        

    }
    
    
    private static void ConvertB3dm(string objPath, string destPath)
    {
        var dir = Path.GetDirectoryName(objPath);
        var name = Path.GetFileNameWithoutExtension(objPath);

        var converter = Converter.MakeDefault();
        var outputFile = dir != null ? Path.Combine(dir, $"{name}.gltf") : $"{name}.gltf";
            
        converter.Convert(objPath, outputFile);

        var glbConv = new Gltf2GlbConverter();
        glbConv.Convert(new Gltf2GlbOptions(outputFile));
        
        File.Delete(outputFile);

        var glbFile = Path.ChangeExtension(outputFile, ".glb");

        var b3dm = new B3dm(File.ReadAllBytes(glbFile));
        
        File.WriteAllBytes(destPath, b3dm.ToBytes());

        //File.Move(Path.ChangeExtension(outputFile, ".glb"), destPath);



    }
}