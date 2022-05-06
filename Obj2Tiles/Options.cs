using CommandLine;

namespace Obj2Tiles;

public class Options
{
    [Option('i', "input", Required = true, HelpText = "Input OBJ file.")]
    public string Input { get; set; }

    [Option('o', "output", Required = true, HelpText = "Output folder.")]
    public string Output { get; set; }

    [Option('s', "stage", Required = false, HelpText = "Stage to stop at.", Default = Stage.Tiles)]
    public Stage StopAt { get; set; }

    [Option('d', "divisions", Required = false, HelpText = "How many tiles divisions", Default = 2)]
    public int Divisions { get; set; }

    [Option('z', "zsplit", Required = false, HelpText = "Adds split along z axis", Default = false)]
    public bool ZSplit { get; set; }
    
    [Option('k', "keeptextures", Required = false, HelpText = "Keeps original textures", Default = false)]
    public bool KeepOriginalTextures { get; set; }
}

public enum Stage
{
    Splitting,
    Decimation,
    Tiles
}