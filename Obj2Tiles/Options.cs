using CommandLine;

namespace Obj2Tiles;

public class Options
{
    [Option('i', "input", Required = true, HelpText = "Input OBJ file.")]
    public string Input { get; set; }

    [Option('o', "output", Required = true, HelpText = "Output folder.")]
    public string Output { get; set; }

    [Option('s', "stage", Required = false, HelpText = "Stage to stop at (Decimation, Splitting, Conversion, Tiling)", Default = Stage.Tiling)]
    public Stage StopAt { get; set; }

    [Option('d', "divisions", Required = false, HelpText = "How many tiles divisions", Default = 2)]
    public int Divisions { get; set; }
    
    [Option('z', "zsplit", Required = false, HelpText = "Splits along z-axis too", Default = false)]
    public bool ZSplit { get; set; }    
    
    [Option('l', "lods", Required = false, HelpText = "How many levels of details", Default = 3)]
    public int LODs { get; set; }

    [Option('a', "auto", Required = false, HelpText = "Automatically estimates the divisions/LODs parameters", Default = false)]
    public bool Auto { get; set; }

    [Option('k', "keeptextures", Required = false, HelpText = "Keeps original textures", Default = false)]
    public bool KeepOriginalTextures { get; set; }
}

public enum Stage
{
    Decimation,
    Splitting,
    Tiling
}