using Newtonsoft.Json;

namespace Obj2Tiles.Stages.Model;

public class Asset
{
    [JsonProperty("version")]
    public string Version { get; set; }
}

public class BoundingVolume
{
    [JsonProperty("box")]
    public double[] Box { get; set; }
}

public class Content
{
    [JsonProperty("uri")]
    public string Uri { get; set; }
}

public class Tileset
{
    [JsonProperty("asset")]
    public Asset Asset { get; set; }

    [JsonProperty("geometricError")]
    public double GeometricError { get; set; }

    [JsonProperty("root")]
    public TileElement Root { get; set; }
}

public class TileElement
{
    [JsonProperty("transform")]
    public double[] Transform { get; set; }

    [JsonProperty("boundingVolume")]
    public BoundingVolume BoundingVolume { get; set; }

    [JsonProperty("geometricError")]
    public double GeometricError { get; set; }

    [JsonProperty("refine")]
    public string Refine { get; set; }

    [JsonProperty("content")]
    public Content Content { get; set; }

    [JsonProperty("children")]
    public List<TileElement> Children { get; set; }
}

