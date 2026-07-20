using System.Text.Json.Serialization;
using CommandLine;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Stages;

namespace Obj2Tiles;

public sealed class Options
{
    [Value(0, MetaName = "Input", Required = true, HelpText = "Input OBJ file.")]
    public string Input { get; set; } = null!;

    [Value(1, MetaName = "Output", Required = true, HelpText = "Output folder.")]
    public string Output { get; set; } = null!;

    [Option('s', "stage", Required = false, HelpText = "Stage to stop at (Decimation, Splitting, Tiling)", Default = Stage.Tiling)]
    public Stage StopAt { get; set; }

    [Option('d', "divisions", Required = false, HelpText = "How many tiles divisions", Default = 2)]
    public int Divisions { get; set; }

    [Option('z', "zsplit", Required = false, HelpText = "Splits along z-axis too", Default = false)]
    public bool ZSplit { get; set; }

    [Option('l', "lods", Required = false, HelpText = "How many levels of details", Default = 3)]
    public int LODs { get; set; }

    [Option('k', "keeptextures", Required = false, HelpText = "Keeps original textures", Default = false)]
    public bool KeepOriginalTextures { get; set; }

    [Option('g', "split-strategy", Required = false, HelpText = "Split strategy: AbsoluteCenter, VertexBaricenter, or VertexMedian (balanced tiles)", Default = SplitPointStrategy.VertexBaricenter)]
    public SplitPointStrategy SplitPointStrategy { get; set; } = SplitPointStrategy.VertexBaricenter;

    [Option("lat", Required = false, HelpText = "Latitude of the mesh", Default = null)]
    public double? Latitude { get; set; }

    [Option("lon", Required = false, HelpText = "Longitude of the mesh", Default = null)]
    public double? Longitude { get; set; }

    [Option("alt", Required = false, HelpText = "Altitude of the mesh (meters)", Default = 0)]
    public double Altitude { get; set; }

    [Option("scale", Required = false, HelpText = "Scale for data if using units other than meters ( 1200.0/3937.0 for survey ft)", Default = 1.0)]
    public double Scale { get; set; }

    [Option('e',"error", Required = false, HelpText = "Base geometric error for the root node, in the model's coordinate units. When 0 (default) it is derived automatically from the model's bounding box diagonal.", Default = 0.0)]
    public double BaseError { get; set; }

    [Option("use-system-temp", Required = false, HelpText = "Uses the system temp folder", Default = false)]
    public bool UseSystemTempFolder { get; set; }

    [Option("keep-intermediate", Required = false, HelpText = "Keeps the intermediate files (do not cleanup)", Default = false)]
    public bool KeepIntermediateFiles { get; set; }

    [Option('t', "y-up-to-z-up", Required = false, HelpText = "Convert the upward Y-axis to the upward Z-axis, which is used in some situations where the upward axis may be the Y-axis or the Z-axis after the obj is exported.", Default = false)]
    public bool YUpToZUp { get; set; }

    [Option("local", Required = false, HelpText = "Local mode: no ECEF geo-referencing, uses identity matrix in tileset.json. Use this when you don't need to place the model on a globe.", Default = false)]
    public bool LocalMode { get; set; }

    [Option("octree", Required = false, HelpText = "Use octree spatial subdivision: each LOD gets one additional division level relative to the next coarser LOD, producing a proper tile hierarchy instead of same-count tiles per LOD.", Default = false)]
    public bool Octree { get; set; }

    [Option("lod-texture-scale", Required = false, HelpText = "Per-LOD texture downscale factor. LOD-0 keeps full resolution (subject to --max-texture-size); each subsequent LOD multiplies the previous resolution by this factor. E.g. 0.5 gives LOD-1 at 1/2 resolution, LOD-2 at 1/4, etc. Use 1.0 to disable per-LOD downscaling.", Default = 0.5)]
    public double LodTextureScale { get; set; }

    [Option("max-texture-size", Required = false, HelpText = "Maximum texture resolution (per side, in pixels) used when repacking/compressing atlases. Larger source textures are downscaled to fit, which bounds the dominant LOD-0 texture cost. 0 disables the cap.", Default = 4096)]
    public int MaxTextureSize { get; set; }

    [Option("texture-quality", Required = false, HelpText = "JPEG quality (1-100) for compressed textures (RepackCompressed and the tileset root). Higher is better quality but larger.", Default = 75)]
    public int TextureQuality { get; set; }

    [Option("texture-format", Required = false, HelpText = "Output image format for repacked/compressed textures: Jpeg (default), Webp or Ktx2. Webp emits the EXT_texture_webp glTF extension (25-35% smaller than JPEG). Ktx2 encodes GPU-compressed Basis Universal textures (KHR_texture_basisu), cutting GPU/VRAM usage ~4-8x, and requires the KTX-Software 'ktx' tool; make sure your renderer supports the chosen format.", Default = TextureFormat.Jpeg)]
    public TextureFormat TextureFormat { get; set; }

    [Option("ktx2-quality", Required = false, HelpText = "KTX2 ETC1S/BasisLZ quality level (1-255, higher is better quality and larger). Reinterpreted as UASTC quality (0-4) when --ktx2-uastc is set. Only used with --texture-format Ktx2.", Default = 128)]
    public int Ktx2Quality { get; set; }

    [Option("ktx2-uastc", Required = false, HelpText = "Use UASTC (higher quality, larger, transcodes to BC7/ASTC) instead of the default ETC1S/BasisLZ mode for KTX2 textures.", Default = false)]
    public bool Ktx2Uastc { get; set; }

    [Option("ktx-path", Required = false, HelpText = "Explicit path to the KTX-Software 'ktx' executable used for --texture-format Ktx2. When omitted it is resolved from the OBJ2TILES_KTX environment variable, next to the executable, or on PATH.", Default = null)]
    public string? KtxPath { get; set; }

    [Option("3tz", Required = false, HelpText = "Produce a single 3D Tiles Archive (.3tz) instead of a loose folder tree. Also enabled automatically when the output path ends with .3tz.", Default = false)]
    public bool ThreeTz { get; set; }

    [Option("3tz-compression", Required = false, HelpText = "DEFLATE level for .3tz output, 0-9 (gzip-style): 0 = stored (no compression), 1-3 = fastest, 4-6 = balanced, 7-9 = smallest. The index is always stored. Zstandard support is planned.", Default = 6)]
    public int ThreeTzCompression { get; set; }
}

public enum Stage
{
    Decimation,
    Splitting,
    Tiling
}