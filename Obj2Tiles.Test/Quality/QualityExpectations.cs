using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Obj2Tiles.Stages;

namespace Obj2Tiles.Test.Quality;

public enum QualityTextureFormat
{
    Jpeg,
    Webp,
    Ktx2,
}

/// <summary>
/// What a produced tileset must satisfy given the CLI options it was generated with.
/// Built from PICT matrix parameters or set explicitly for curated real-world cases.
/// </summary>
public sealed class QualityExpectations
{
    public Stage Stage = Stage.Tiling;
    public int Lods = 3;
    public int Divisions = 2;
    public bool ZSplit;
    public bool Octree;
    public bool KeepTextures;
    public bool SingleMaterialPerPart;
    public bool Local;
    public bool NoRootContent;
    /// <summary>Uniform scale baked into the georeference transform (--scale). 1 = meters.</summary>
    public double Scale = 1.0;
    public QualityTextureFormat TextureFormat = QualityTextureFormat.Jpeg;
    public int MaxTextureSize;
    public double LodTextureScale = 1.0;
    public bool Output3tz;
    public long BudgetBytes = 30L * 1024 * 1024;

    /// <summary>Texture format/size assertions only make sense for repacked textures.</summary>
    public bool CheckTextures => Stage == Stage.Tiling && !KeepTextures;

    public static QualityExpectations FromParams(IReadOnlyDictionary<string, string> p)
    {
        var e = new QualityExpectations
        {
            Stage = Enum.Parse<Stage>(p.GetValueOrDefault("stage", "Tiling"), true),
            Lods = int.Parse(p.GetValueOrDefault("lods", "3")),
            Divisions = int.Parse(p.GetValueOrDefault("divisions", "2")),
            ZSplit = IsTrue(p, "zsplit"),
            Octree = IsTrue(p, "octree"),
            KeepTextures = IsTrue(p, "keeptex"),
            SingleMaterialPerPart = IsTrue(p, "smp"),
            Local = p.GetValueOrDefault("geo", "georef") == "local",
            NoRootContent = IsTrue(p, "noroot"),
            TextureFormat = Enum.Parse<QualityTextureFormat>(p.GetValueOrDefault("texfmt", "Jpeg"), true),
            MaxTextureSize = int.Parse(p.GetValueOrDefault("maxtex", "0")),
            LodTextureScale = double.Parse(p.GetValueOrDefault("lodscale", "1"),
                System.Globalization.CultureInfo.InvariantCulture),
            Output3tz = p.GetValueOrDefault("outform", "dir") == "3tz",
            Scale = ParseScale(p.GetValueOrDefault("scale")),
        };

        return e;
    }

    private static double ParseScale(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && Obj2Tiles.Options.TryParseScale(raw, out var v, out _) ? v : 1.0;

    private static bool IsTrue(IReadOnlyDictionary<string, string> p, string key) =>
        p.GetValueOrDefault(key, "false").Equals("true", StringComparison.OrdinalIgnoreCase);
}

/// <summary>One generated (PICT) positive CLI case.</summary>
public sealed class MatrixCase
{
    [JsonProperty("id")] public string Id { get; set; } = "";
    [JsonProperty("dataset")] public string Dataset { get; set; } = "cube-textured";
    [JsonProperty("args")] public List<string> Args { get; set; } = new();
    [JsonProperty("outform")] public string Outform { get; set; } = "dir";
    [JsonProperty("outName")] public string OutName { get; set; } = "out";
    [JsonProperty("params")] public Dictionary<string, string> Params { get; set; } = new();

    public QualityExpectations Expectations => QualityExpectations.FromParams(Params);
}

/// <summary>One curated invalid-configuration case (must be rejected by the CLI).</summary>
public sealed class NegativeCase
{
    [JsonProperty("name")] public string Name { get; set; } = "";
    [JsonProperty("args")] public List<string> Args { get; set; } = new();
}

public static class QualityFixtures
{
    public static List<MatrixCase> LoadMatrix() => Load("matrix.json")
        .CaselessDeserialize<MatrixSuite>()?.cases
        ?? throw new InvalidDataException("quality/matrix.json missing or invalid");

    public static List<NegativeCase> LoadNegatives() => (Load("negatives.json")
        .CaselessDeserialize<List<NegativeCase>>())
        ?? throw new InvalidDataException("quality/negatives.json missing or invalid");

    private static string Load(string file)
    {
        var path = Path.Combine(CliHarness.TestDataRoot, "quality", file);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Quality gate fixture '{file}' was not copied to the test output. " +
                                            $"Expected at {path}", path);
        return File.ReadAllText(path);
    }

    public static QualityDataset DatasetFor(string name) => name switch
    {
        "cube-textured" => QualityDataset.CubeTextured(),
        "brighton" => QualityDataset.Brighton(),
        "odm-textured" => QualityDataset.OdmTextured(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown quality gate dataset"),
    };
}

// Newtonsoft helper shim: case-insensitive match for the whole document.
file static class Extenders
{
    public static T? CaselessDeserialize<T>(this string json) where T : class
    {
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
        };
        return JsonConvert.DeserializeObject<T>(json, settings);
    }
}

internal sealed class MatrixSuite
{
    [JsonProperty("cases")] public List<MatrixCase> cases { get; set; } = new();
}
