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
                
                //var outputFile = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(file), ".b3dm"));
                //var obj = TilesConverter.WriteB3dm(file, outputFile, null);
                
                //Debug.WriteLine(obj.ToString());
                
                //var outputFile = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(file), ".gltf"));
                //var opts = new GltfOptions { Binary = false, WithBatchTable = false, ObjEncoding = Encoding.UTF8};
                //var converter = new Converter(file, opts);
                //converter.WriteFile(outputFile2);
                
                //var converter = Converter.MakeDefault();
                //converter.Convert(file, outputFile);
                var outputFile = Path.Combine(outputFolder, Path.ChangeExtension(Path.GetFileName(file), ".b3dm"));
                ConvertB3dm(file, outputFile);
                
                File.Copy(Path.ChangeExtension(file, ".json"), Path.ChangeExtension(outputFile, ".json"));

                //var glbConv = new Gltf2GlbConverter();
                //glbConv.Convert(new Gltf2GlbOptions(outputFile));

            }

        }
        

        //var converter = new TilesConverter(Path.Combine(sourcePath, "LOD-0"), destPath, new GisPosition());

        //converter.Run();


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