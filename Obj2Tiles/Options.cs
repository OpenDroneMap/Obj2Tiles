using System.Text.Json.Serialization;
using CommandLine;

namespace Obj2Tiles;

public class Options
{
    [Option('i', "input", Required = true, HelpText = "Input OBJ file.")]
    public string Input { get; set; }

    [Option('o', "output", Required = true, HelpText = "Output folder.")]
    public string Output { get; set; }

    [Option('s', "stage", Required = false, HelpText = "Stage to stop at (Decimation, Splitting, Tiling)", Default = Stage.Tiling)]
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
    
    [Option("lat", Required = false, HelpText = "Latitude of the mesh", Default = null)]
    public double? Latitude { get; set; }
    
    [Option("lon", Required = false, HelpText = "Longitude of the mesh", Default = null)]
    public double? Longitude { get; set; }
    
    [Option("alt", Required = false, HelpText = "Altitude of the mesh", Default = null)]
    public double? Altitude { get; set; }
    
    [Option('v', "verbose", Required = false, HelpText = "Verbose output", Default = false)]
    public bool Verbose { get; set; }
    
    [Option("use-system-temp", Required = false, HelpText = "Uses the system temp folder", Default = false)]
    public bool UseSystemTempFolder { get; set; }
}

public enum Stage
{
    Decimation,
    Splitting,
    Tiling
}