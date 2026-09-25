using System.Diagnostics;
using System.Globalization;
using Obj2Tiles.Library.Algos;
using Obj2Tiles.Library.Materials;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using PackingRectangle = Obj2Tiles.Library.Algos.Model.Rectangle;
using Path = System.IO.Path;

namespace Obj2Tiles.Library.Geometry;

public class MeshT : IMesh
{
    private List<Vertex3> _vertices;
    private List<Vertex2> _textureVertices;
    private readonly List<FaceT> _faces;
    private List<Material> _materials;
    private List<RGB>? _vertexColors;

    public IReadOnlyList<Vertex3> Vertices => _vertices;
    public IReadOnlyList<Vertex2> TextureVertices => _textureVertices;
    public IReadOnlyList<FaceT> Faces => _faces;
    public IReadOnlyList<Material> Materials => _materials;
    public IReadOnlyList<RGB>? VertexColors => _vertexColors;

    public void Translate(Vertex3 offset)
    {
        for (var i = 0; i < _vertices.Count; i++)
            _vertices[i] = _vertices[i] + offset;
    }

    public const string DefaultName = "Mesh";
    private const int Padding = 2;     // Bleed ring (pixels) added around atlas charts to hide bilinear sampling at UV seams.

    private const int MaximumAtlasTextureSize = 16384; // Maximum Size Single Material Texture can grow too.

    // Binary-search tuning for the single-atlas chart scale search.
    private const int ScaleSearchMaxIterations = 16; // Hard cap on binary-search probes.
    private const double ScaleSearchTolerance = 0.0067d; // Stop when the scale window is below this.

    public string Name { get; set; } = DefaultName;
    public string DebugName { get; set; } = string.Empty;

    public TexturesStrategy TexturesStrategy { get; set; }

    /// <summary>
    /// Multiplier applied to the atlas edge length during texture repacking.
    /// 1.0 = full resolution, 0.5 = half, 0.25 = quarter, etc.
    /// Values are clamped to (0, 1]. Only has effect with Repack or RepackCompressed.
    /// </summary>
    public float TextureDownscale { get; set; } = 1.0f;

    /// <summary>
    /// Maximum source texture resolution (per side, in pixels) used when repacking or compressing
    /// atlases. Larger textures are downscaled to fit. 0 disables the cap.
    /// </summary>
    public int MaxTextureSize { get; set; } = 0;

    /// <summary>
    /// JPEG quality (1-100) used when saving compressed textures (RepackCompressed and Compress).
    /// </summary>
    public int TextureQuality { get; set; } = 75;

    /// <summary>
    /// Output image format for repacked/compressed textures. Webp emits the EXT_texture_webp glTF
    /// extension and is typically 25-35% smaller than JPEG at comparable quality.
    /// </summary>
    public TextureFormat TextureFormat { get; set; } = TextureFormat.Jpeg;

    /// <summary>
    /// Repackages all material maps used by this mesh into one atlas per map and emits one material.
    /// </summary>
    public bool SingleMaterialPerPart { get; set; }

    public MeshT(IEnumerable<Vertex3> vertices, IEnumerable<Vertex2> textureVertices,
        IEnumerable<FaceT> faces, IEnumerable<Material> materials, IEnumerable<RGB>? vertexColors = null)
    {
        _vertices = [.. vertices];
        _textureVertices = [.. textureVertices];
        _faces = [.. faces];
        _materials = [.. materials];
        _vertexColors = vertexColors != null ? [.. vertexColors] : null;
    }

    public int Split(IVertexUtils utils, double q, out IMesh left,
        out IMesh right)
    {
        var leftVertices = new Dictionary<Vertex3, int>(_vertices.Count);
        var rightVertices = new Dictionary<Vertex3, int>(_vertices.Count);

        var leftFaces = new List<FaceT>(_faces.Count);
        var rightFaces = new List<FaceT>(_faces.Count);

        var leftTextureVertices = new Dictionary<Vertex2, int>(_textureVertices.Count);
        var rightTextureVertices = new Dictionary<Vertex2, int>(_textureVertices.Count);

        var hasColors = _vertexColors != null;
        var leftColors = hasColors ? new List<RGB>(_vertices.Count) : null;
        var rightColors = hasColors ? new List<RGB>(_vertices.Count) : null;

        var count = 0;

        for (var index = 0; index < _faces.Count; index++)
        {
            var face = _faces[index];

            var vA = _vertices[face.IndexA];
            var vB = _vertices[face.IndexB];
            var vC = _vertices[face.IndexC];

            var vtA = _textureVertices[face.TextureIndexA];
            var vtB = _textureVertices[face.TextureIndexB];
            var vtC = _textureVertices[face.TextureIndexC];

            var aSide = utils.GetDimension(vA) < q;
            var bSide = utils.GetDimension(vB) < q;
            var cSide = utils.GetDimension(vC) < q;

            if (aSide)
            {
                if (bSide)
                {
                    if (cSide)
                    {
                        // All on the left

                        AddVertexWithColor(leftVertices, leftColors, vA, face.IndexA);
                        AddVertexWithColor(leftVertices, leftColors, vB, face.IndexB);
                        AddVertexWithColor(leftVertices, leftColors, vC, face.IndexC);

                        var indexALeft = leftVertices[vA];
                        var indexBLeft = leftVertices[vB];
                        var indexCLeft = leftVertices[vC];

                        var indexATextureLeft = leftTextureVertices!.AddIndex(vtA);
                        var indexBTextureLeft = leftTextureVertices!.AddIndex(vtB);
                        var indexCTextureLeft = leftTextureVertices!.AddIndex(vtC);

                        leftFaces.Add(new FaceT(indexALeft, indexBLeft, indexCLeft,
                            indexATextureLeft, indexBTextureLeft, indexCTextureLeft,
                            face.MaterialIndex));
                    }
                    else
                    {
                        IntersectRight2DWithTexture(utils, q, face.IndexC, face.IndexA, face.IndexB,
                            leftVertices,
                            rightVertices,
                            leftColors, rightColors,
                            face.TextureIndexC, face.TextureIndexA, face.TextureIndexB,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces, rightFaces
                        );
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectRight2DWithTexture(utils, q, face.IndexB, face.IndexC, face.IndexA,
                            leftVertices,
                            rightVertices,
                            leftColors, rightColors,
                            face.TextureIndexB, face.TextureIndexC, face.TextureIndexA,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2DWithTexture(utils, q, face.IndexA, face.IndexB, face.IndexC,
                            leftVertices,
                            rightVertices,
                            leftColors, rightColors,
                            face.TextureIndexA, face.TextureIndexB, face.TextureIndexC,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                }
            }
            else
            {
                if (bSide)
                {
                    if (cSide)
                    {
                        IntersectRight2DWithTexture(utils, q, face.IndexA, face.IndexB, face.IndexC,
                            leftVertices,
                            rightVertices,
                            leftColors, rightColors,
                            face.TextureIndexA, face.TextureIndexB, face.TextureIndexC,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2DWithTexture(utils, q, face.IndexB, face.IndexC, face.IndexA,
                            leftVertices,
                            rightVertices,
                            leftColors, rightColors,
                            face.TextureIndexB, face.TextureIndexC, face.TextureIndexA,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectLeft2DWithTexture(utils, q, face.IndexC, face.IndexA, face.IndexB,
                            leftVertices,
                            rightVertices,
                            leftColors, rightColors,
                            face.TextureIndexC, face.TextureIndexA, face.TextureIndexB,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        // All on the right

                        AddVertexWithColor(rightVertices, rightColors, vA, face.IndexA);
                        AddVertexWithColor(rightVertices, rightColors, vB, face.IndexB);
                        AddVertexWithColor(rightVertices, rightColors, vC, face.IndexC);

                        var indexARight = rightVertices[vA];
                        var indexBRight = rightVertices[vB];
                        var indexCRight = rightVertices[vC];

                        var indexATextureRight = rightTextureVertices!.AddIndex(vtA);
                        var indexBTextureRight = rightTextureVertices!.AddIndex(vtB);
                        var indexCTextureRight = rightTextureVertices!.AddIndex(vtC);

                        rightFaces.Add(new FaceT(indexARight, indexBRight, indexCRight,
                            indexATextureRight, indexBTextureRight, indexCTextureRight,
                            face.MaterialIndex));
                    }
                }
            }
        }

        var orderedLeftVertices = leftVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var orderedRightVertices = rightVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var rightMaterials = _materials.Select(mat => (Material)mat.Clone());

        var orderedLeftTextureVertices = leftTextureVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var orderedRightTextureVertices = rightTextureVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var leftMaterials = _materials.Select(mat => (Material)mat.Clone());

        left = new MeshT(orderedLeftVertices, orderedLeftTextureVertices, leftFaces, leftMaterials, leftColors)
        {
            Name = $"{Name}-{utils.Axis}L"
        };
        right = new MeshT(orderedRightVertices, orderedRightTextureVertices, rightFaces, rightMaterials, rightColors)
        {
            Name = $"{Name}-{utils.Axis}R"
        };

        return count;
    }

    /// <summary>
    /// Adds a vertex to the dictionary and appends its color to the parallel list if the vertex is new.
    /// </summary>
    private void AddVertexWithColor(IDictionary<Vertex3, int> vertices, List<RGB>? colors,
        Vertex3 vertex, int sourceIndex)
    {
        var prevCount = vertices.Count;
        vertices.AddIndex(vertex);
        if (colors != null && vertices.Count > prevCount)
            colors.Add(_vertexColors![sourceIndex]);
    }

    /// <summary>
    /// Adds an interpolated intersection vertex and its interpolated color.
    /// </summary>
    private static void AddIntersectionVertexWithColor(IDictionary<Vertex3, int> vertices, List<RGB>? colors,
        Vertex3 vertex, RGB? color)
    {
        var prevCount = vertices.Count;
        vertices.AddIndex(vertex);
        if (colors != null && vertices.Count > prevCount)
            colors.Add(color!);
    }

    private void IntersectLeft2DWithTexture(IVertexUtils utils, double q, int indexVL,
        int indexVR1, int indexVR2,
        IDictionary<Vertex3, int> leftVertices, IDictionary<Vertex3, int> rightVertices,
        List<RGB>? leftColors, List<RGB>? rightColors,
        int indexTextureVL, int indexTextureVR1, int indexTextureVR2,
        IDictionary<Vertex2, int> leftTextureVertices, IDictionary<Vertex2, int> rightTextureVertices,
        int materialIndex, ICollection<FaceT> leftFaces, ICollection<FaceT> rightFaces)
    {
        var vL = _vertices[indexVL];
        var vR1 = _vertices[indexVR1];
        var vR2 = _vertices[indexVR2];

        var tVL = _textureVertices[indexTextureVL];
        var tVR1 = _textureVertices[indexTextureVR1];
        var tVR2 = _textureVertices[indexTextureVR2];

        AddVertexWithColor(leftVertices, leftColors, vL, indexVL);
        var indexVLLeft = leftVertices[vL];
        var indexTextureVLLeft = leftTextureVertices.AddIndex(tVL);

        if (Math.Abs(utils.GetDimension(vR1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vR2) - q) < Common.Epsilon)
        {
            // Right Vertices are on the line

            AddVertexWithColor(leftVertices, leftColors, vR1, indexVR1);
            AddVertexWithColor(leftVertices, leftColors, vR2, indexVR2);
            var indexVR1Left = leftVertices[vR1];
            var indexVR2Left = leftVertices[vR2];

            var indexTextureVR1Left = leftTextureVertices.AddIndex(tVR1);
            var indexTextureVR2Left = leftTextureVertices.AddIndex(tVR2);

            leftFaces.Add(new FaceT(indexVLLeft, indexVR1Left, indexVR2Left,
                indexTextureVLLeft, indexTextureVR1Left, indexTextureVR2Left, materialIndex));

            return;
        }

        AddVertexWithColor(rightVertices, rightColors, vR1, indexVR1);
        AddVertexWithColor(rightVertices, rightColors, vR2, indexVR2);
        var indexVR1Right = rightVertices[vR1];
        var indexVR2Right = rightVertices[vR2];

        // a on the left, b and c on the right

        // Prima intersezione
        var t1 = utils.CutEdge(vL, vR1, q);
        RGB? t1Color = null;
        if (_vertexColors != null)
        {
            var perc1Color = Common.GetIntersectionPerc(vL, vR1, t1);
            t1Color = _vertexColors[indexVL].CutEdgePerc(_vertexColors[indexVR1], perc1Color);
        }
        AddIntersectionVertexWithColor(leftVertices, leftColors, t1, t1Color);
        AddIntersectionVertexWithColor(rightVertices, rightColors, t1, t1Color);
        var indexT1Left = leftVertices[t1];
        var indexT1Right = rightVertices[t1];

        // Seconda intersezione
        var t2 = utils.CutEdge(vL, vR2, q);
        RGB? t2Color = null;
        if (_vertexColors != null)
        {
            var perc2Color = Common.GetIntersectionPerc(vL, vR2, t2);
            t2Color = _vertexColors[indexVL].CutEdgePerc(_vertexColors[indexVR2], perc2Color);
        }
        AddIntersectionVertexWithColor(leftVertices, leftColors, t2, t2Color);
        AddIntersectionVertexWithColor(rightVertices, rightColors, t2, t2Color);
        var indexT2Left = leftVertices[t2];
        var indexT2Right = rightVertices[t2];

        // Split texture
        var indexTextureVR1Right = rightTextureVertices.AddIndex(tVR1);
        var indexTextureVR2Right = rightTextureVertices.AddIndex(tVR2);

        var perc1 = Common.GetIntersectionPerc(vL, vR1, t1);

        // Prima intersezione texture
        var t1t = tVL.CutEdgePerc(tVR1, perc1);
        var indexTextureT1Left = leftTextureVertices.AddIndex(t1t);
        var indexTextureT1Right = rightTextureVertices.AddIndex(t1t);

        var perc2 = Common.GetIntersectionPerc(vL, vR2, t2);

        // Seconda intersezione texture
        var t2t = tVL.CutEdgePerc(tVR2, perc2);
        var indexTextureT2Left = leftTextureVertices.AddIndex(t2t);
        var indexTextureT2Right = rightTextureVertices.AddIndex(t2t);

        var lface = new FaceT(indexVLLeft, indexT1Left, indexT2Left,
            indexTextureVLLeft, indexTextureT1Left, indexTextureT2Left, materialIndex);
        leftFaces.Add(lface);

        var rface1 = new FaceT(indexT1Right, indexVR1Right, indexVR2Right,
            indexTextureT1Right, indexTextureVR1Right, indexTextureVR2Right, materialIndex);
        rightFaces.Add(rface1);

        var rface2 = new FaceT(indexT1Right, indexVR2Right, indexT2Right,
            indexTextureT1Right, indexTextureVR2Right, indexTextureT2Right, materialIndex);
        rightFaces.Add(rface2);
    }

    private void IntersectRight2DWithTexture(IVertexUtils utils, double q, int indexVR,
        int indexVL1, int indexVL2,
        IDictionary<Vertex3, int> leftVertices, IDictionary<Vertex3, int> rightVertices,
        List<RGB>? leftColors, List<RGB>? rightColors,
        int indexTextureVR, int indexTextureVL1, int indexTextureVL2,
        IDictionary<Vertex2, int> leftTextureVertices, IDictionary<Vertex2, int> rightTextureVertices,
        int materialIndex, ICollection<FaceT> leftFaces, ICollection<FaceT> rightFaces)
    {
        var vR = _vertices[indexVR];
        var vL1 = _vertices[indexVL1];
        var vL2 = _vertices[indexVL2];

        var tVR = _textureVertices[indexTextureVR];
        var tVL1 = _textureVertices[indexTextureVL1];
        var tVL2 = _textureVertices[indexTextureVL2];

        AddVertexWithColor(rightVertices, rightColors, vR, indexVR);
        var indexVRRight = rightVertices[vR];
        var indexTextureVRRight = rightTextureVertices.AddIndex(tVR);

        if (Math.Abs(utils.GetDimension(vL1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vL2) - q) < Common.Epsilon)
        {
            // Left Vertices are on the line

            AddVertexWithColor(rightVertices, rightColors, vL1, indexVL1);
            AddVertexWithColor(rightVertices, rightColors, vL2, indexVL2);
            var indexVL1Right = rightVertices[vL1];
            var indexVL2Right = rightVertices[vL2];

            var indexTextureVL1Right = rightTextureVertices.AddIndex(tVL1);
            var indexTextureVL2Right = rightTextureVertices.AddIndex(tVL2);

            rightFaces.Add(new FaceT(indexVRRight, indexVL1Right, indexVL2Right,
                indexTextureVRRight, indexTextureVL1Right, indexTextureVL2Right, materialIndex));

            return;
        }

        AddVertexWithColor(leftVertices, leftColors, vL1, indexVL1);
        AddVertexWithColor(leftVertices, leftColors, vL2, indexVL2);
        var indexVL1Left = leftVertices[vL1];
        var indexVL2Left = leftVertices[vL2];

        // a on the right, b and c on the left

        // Prima intersezione
        var t1 = utils.CutEdge(vR, vL1, q);
        RGB? t1Color = null;
        if (_vertexColors != null)
        {
            var perc1Color = Common.GetIntersectionPerc(vR, vL1, t1);
            t1Color = _vertexColors[indexVR].CutEdgePerc(_vertexColors[indexVL1], perc1Color);
        }
        AddIntersectionVertexWithColor(leftVertices, leftColors, t1, t1Color);
        AddIntersectionVertexWithColor(rightVertices, rightColors, t1, t1Color);
        var indexT1Left = leftVertices[t1];
        var indexT1Right = rightVertices[t1];

        // Seconda intersezione
        var t2 = utils.CutEdge(vR, vL2, q);
        RGB? t2Color = null;
        if (_vertexColors != null)
        {
            var perc2Color = Common.GetIntersectionPerc(vR, vL2, t2);
            t2Color = _vertexColors[indexVR].CutEdgePerc(_vertexColors[indexVL2], perc2Color);
        }
        AddIntersectionVertexWithColor(leftVertices, leftColors, t2, t2Color);
        AddIntersectionVertexWithColor(rightVertices, rightColors, t2, t2Color);
        var indexT2Left = leftVertices[t2];
        var indexT2Right = rightVertices[t2];

        // Split texture
        var indexTextureVL1Left = leftTextureVertices.AddIndex(tVL1);
        var indexTextureVL2Left = leftTextureVertices.AddIndex(tVL2);

        var perc1 = Common.GetIntersectionPerc(vR, vL1, t1);

        // Prima intersezione texture
        var t1t = tVR.CutEdgePerc(tVL1, perc1);
        var indexTextureT1Left = leftTextureVertices.AddIndex(t1t);
        var indexTextureT1Right = rightTextureVertices.AddIndex(t1t);

        var perc2 = Common.GetIntersectionPerc(vR, vL2, t2);

        // Seconda intersezione texture
        var t2t = tVR.CutEdgePerc(tVL2, perc2);
        var indexTextureT2Left = leftTextureVertices.AddIndex(t2t);
        var indexTextureT2Right = rightTextureVertices.AddIndex(t2t);

        var rface = new FaceT(indexVRRight, indexT1Right, indexT2Right,
            indexTextureVRRight, indexTextureT1Right, indexTextureT2Right, materialIndex);
        rightFaces.Add(rface);

        var lface1 = new FaceT(indexT2Left, indexVL1Left, indexVL2Left,
            indexTextureT2Left, indexTextureVL1Left, indexTextureVL2Left, materialIndex);
        leftFaces.Add(lface1);

        var lface2 = new FaceT(indexT2Left, indexT1Left, indexVL1Left,
            indexTextureT2Left, indexTextureT1Left, indexTextureVL1Left, materialIndex);
        leftFaces.Add(lface2);
    }

    private void TrimTextures(string targetFolder)
    {
        var tasks = new List<Task>();

        LoadTexturesCache();

        var facesByMaterial = GetFacesByMaterial();

        var newTextureVertices = new Dictionary<Vertex2, int>(_textureVertices.Count);

        var canonicalIndex = GetCanonicalPositionIndices();

        for (var m = 0; m < facesByMaterial.Count; m++)
        {
            var material = _materials[m];
            var facesIndexes = facesByMaterial[m];

            if (facesIndexes.Count == 0)
                continue;

            var edgesMapper = GetEdgesMapper(facesIndexes, canonicalIndex);
            var facesMapper = GetFacesMapper(edgesMapper);
            var clusters = GetFacesClusters(facesIndexes, facesMapper);

            // Sort clusters by count (improves packing density, could be removed if we notice a bottleneck)
            clusters.Sort((a, b) => b.Count.CompareTo(a.Count));

            BinPackTextures(targetFolder, m, clusters, newTextureVertices, tasks);
        }

        _textureVertices = newTextureVertices.OrderBy(item => item.Value).Select(item => item.Key).ToList();

        var allSaves = Task.WhenAll(tasks);
        var saveSw = Stopwatch.StartNew();
        long nextSaveProgressMs = 5000;
        while (!allSaves.Wait(100))
        {
            if (saveSw.ElapsedMilliseconds >= nextSaveProgressMs)
            {
                Console.WriteLine($" -> [{DebugName}] Saving texture atlases... ({saveSw.Elapsed.TotalSeconds:F0}s)");
                nextSaveProgressMs += 5000;
            }
        }
    }

    // Exporters (Blender in particular) commonly emit a separate position vertex per
    // unique (position, normal, uv) corner, so the SAME 3D point can appear under many
    // different vertex indices. Canonicalizing by value once - now that Vertex3.Equals
    // is correct - means the position-edge adjacency check in GetEdgesMapper recognizes
    // two triangles as sharing an edge whenever they're at the same actual location,
    // regardless of which duplicate index each one happens to reference. Without this,
    // position-edge matching on raw indices sees almost no adjacency at all on meshes
    // like this, shattering every UV island into near-single-triangle fragments.
    private int[] GetCanonicalPositionIndices()
    {
        var canonicalPosition = new Dictionary<Vertex3, int>(_vertices.Count);
        var canonicalIndex = new int[_vertices.Count];
        for (var i = 0; i < _vertices.Count; i++)
        {
            if (!canonicalPosition.TryGetValue(_vertices[i], out var canonical))
            {
                canonical = i;
                canonicalPosition[_vertices[i]] = canonical;
            }
            canonicalIndex[i] = canonical;
        }

        return canonicalIndex;
    }

    private sealed class SingleAtlasChart
    {
        public Material Material { get; }
        public List<int> FaceIndexes { get; }
        public RectangleF UvBounds { get; }
        public Image<Rgba32>? DiffuseTexture { get; }
        public Image<Rgba32>? NormalTexture { get; }
        public int NaturalWidth { get; }
        public int NaturalHeight { get; }

        /// <summary>
        /// Bleed ring (pixels) reserved around the chart in the atlas.
        /// </summary>
        public int Padding { get; }

        /// <summary>
        /// Padding actually used by the selected packing scale. Charts with either dimension smaller
        /// than one texel do not receive a bleed ring: there is no texel detail to protect, and the
        /// ring would disproportionately consume atlas space.
        /// </summary>
        public int EffectivePadding { get; set; }

        /// <summary>
        /// True when the packer placed the chart rotated 90° clockwise; the blit and the UV remap
        /// account for it.
        /// </summary>
        public bool Rotated { get; set; }

        public PackingRectangle PackedRectangle { get; set; } = new();

        public SingleAtlasChart(Material material, List<int> faceIndexes, RectangleF uvBounds,
            Image<Rgba32>? diffuseTexture, Image<Rgba32>? normalTexture, int naturalWidth, int naturalHeight,
            int padding)
        {
            Material = material;
            FaceIndexes = faceIndexes;
            UvBounds = uvBounds;
            DiffuseTexture = diffuseTexture;
            NormalTexture = normalTexture;
            NaturalWidth = naturalWidth;
            NaturalHeight = naturalHeight;
            Padding = padding;
            EffectivePadding = padding;
        }
    }

    /// <summary>
    /// Packs every material used by this mesh into one atlas.
    /// </summary>
    private void MergeMaterialsIntoSingleAtlas(string targetFolder)
    {
        LoadTexturesCache();

        var facesByMaterial = GetFacesByMaterial();
        var usedMaterials = new List<Material>();
        var hasNormalMap = false;

        // Bleed ring reserved around each chart. Stored per chart so it can potentially become per-chart in the
        // future. Currently defined by Padding const.
        var chartPadding = MaxTextureSize > 0
            ? Math.Min(Padding, Math.Max(0, (MaxTextureSize - 1) / 2))
            : Padding;

        // First pass: collect the chart data and each chart's source texel density (px per UV unit).
        var chartData = new List<(Material Material, List<int> FaceIndexes, RectangleF UvBounds,
            Image<Rgba32>? Diffuse, Image<Rgba32>? Normal, double DensityU, double DensityV)>();
        var maxSourceDensity = 0.0;
        var canonicalIndex = GetCanonicalPositionIndices();

        for (var materialIndex = 0; materialIndex < facesByMaterial.Count; materialIndex++)
        {
            var faceIndexes = facesByMaterial[materialIndex];
            if (faceIndexes.Count == 0)
                continue;

            var material = _materials[materialIndex];
            usedMaterials.Add(material);

            var diffuse = material.Texture != null ? TexturesCache.GetTexture(material.Texture) : null;
            var normal = material.NormalMap != null ? TexturesCache.GetTexture(material.NormalMap) : null;
            hasNormalMap |= normal != null;

            var edgesMapper = GetEdgesMapper(faceIndexes, canonicalIndex);
            var facesMapper = GetFacesMapper(edgesMapper);
            var clusters = GetFacesClusters(faceIndexes, facesMapper);

            foreach (var cluster in clusters)
            {
                var bounds = GetClusterRect(cluster);
                var referenceTexture = diffuse ?? normal;

                // The chart's native texel density is its source texture's resolution, per UV axis
                double densityU = 0d, densityV = 0d;
                if (referenceTexture != null)
                {
                    densityU = referenceTexture.Width;
                    densityV = referenceTexture.Height;
                    maxSourceDensity = Math.Max(maxSourceDensity, Math.Max(densityU, densityV));
                }

                chartData.Add((material, cluster, bounds, diffuse, normal, densityU, densityV));
            }
        }

        if (chartData.Count == 0)
            return;

        // Every chart keeps its own native per-axis texel density; targetDensity is a shared multiplier
        // (user downscale, plus a shrink factor when the largest chart would not fit the capped atlas).
        var targetDensity = (double)Math.Clamp(TextureDownscale, float.Epsilon, 1.0f);

        if (MaxTextureSize > 0)
        {
            // The largest chart (including padding) must fit the capped atlas edge.
            double maxScaledSpan = 0d;
            foreach (var data in chartData)
            {
                if (data.DensityU > 0)
                {
                    maxScaledSpan = Math.Max(maxScaledSpan,
                        Math.Max(data.UvBounds.Width * data.DensityU, data.UvBounds.Height * data.DensityV));
                }
            }

            if (maxScaledSpan > 0)
            {
                int available = Math.Max(1, MaxTextureSize - 2 * chartPadding);

                if (maxScaledSpan > available)
                {
                    targetDensity = available / maxScaledSpan;
                }
            }
        }

        var charts = new List<SingleAtlasChart>(chartData.Count);
        foreach (var data in chartData)
        {
            int naturalWidth, naturalHeight;
            if (data.DensityU > 0 || data.DensityV > 0)
            {
                naturalWidth = Math.Max(1, (int)Math.Round(data.UvBounds.Width * data.DensityU * targetDensity));
                naturalHeight = Math.Max(1, (int)Math.Round(data.UvBounds.Height * data.DensityV * targetDensity));
            }
            else
            {
                // Texture-less charts carry no texel data: a single solid-color pixel is enough.
                naturalWidth = 1;
                naturalHeight = 1;
            }

            charts.Add(new SingleAtlasChart(data.Material, data.FaceIndexes, data.UvBounds,
                       data.Diffuse, data.Normal, naturalWidth, naturalHeight, chartPadding));
        }

        // Sort charts by padded area (descending) for better skyline packing.
        charts.Sort((a, b) =>
        {
            long areaA = (a.NaturalWidth + 2 * a.Padding) * (a.NaturalHeight + 2 * a.Padding);
            long areaB = (b.NaturalWidth + 2 * b.Padding) * (b.NaturalHeight + 2 * b.Padding);
            return areaB.CompareTo(areaA);
        });

        // Estimate the atlas edge from the padded chart sizes.
        long estimatedArea = 0;
        var largestPaddedDimension = 1;
        foreach (var chart in charts)
        {
            var width = chart.NaturalWidth + 2 * chart.Padding;
            var height = chart.NaturalHeight + 2 * chart.Padding;
            estimatedArea += (long)width * height;
            largestPaddedDimension = Math.Max(largestPaddedDimension, Math.Max(width, height));
        }

        // Hard ceiling for the atlas edge: the user cap when set, otherwise the built-in maximum.
        int atlasEdgeCeiling = MaxTextureSize > 0
            ? Math.Min(MaxTextureSize, MaximumAtlasTextureSize)
            : MaximumAtlasTextureSize;
        var estimatedEdge = Math.Max(largestPaddedDimension, (int)Math.Ceiling(Math.Sqrt(estimatedArea)));
        var atlasEdge = Math.Min(Math.Max(32, Common.NextPowerOfTwo(estimatedEdge)), atlasEdgeCeiling);

        // Packs every chart into an atlas of the given edge length at the given uniform scale using
        // the skyline algorithm (rotations allowed).
        bool TryPack(double scale, int edge,
                     out PackingRectangle?[]? placements,
                     out bool[]? rotated, out float occupancy)
        {
            var sizes = new (int Width, int Height)[charts.Count];
            for (var i = 0; i < charts.Count; i++)
            {
                var chart = charts[i];
                var padding = chart.NaturalWidth * scale < 1.0 || chart.NaturalHeight * scale < 1.0
                    ? 0
                    : chart.Padding;
                sizes[i] = (Math.Max(1, (int)Math.Round(chart.NaturalWidth * scale)) + 2 * padding,
                    Math.Max(1, (int)Math.Round(chart.NaturalHeight * scale)) + 2 * padding);
            }

            SkylineBinPack packer = new SkylineBinPack(edge, edge, true);
            placements = new PackingRectangle?[charts.Count];
            rotated = new bool[charts.Count];

            // Insert charts one-by-one in pre-sorted order (largest area first).
            for (var i = 0; i < charts.Count; i++)
            {
                var (w, h) = sizes[i];
                var rect = packer.Insert(w, h, out var wasRotated);
                if (rect.Height == 0)
                {
                    placements[i] = null;
                    rotated[i] = false;
                }
                else
                {
                    placements[i] = rect;
                    rotated[i] = wasRotated;
                }
            }

            occupancy = packer.Occupancy();

            for (var i = 0; i < placements.Length; i++)
            {
                if (placements[i] == null)
                    return false;
            }

            return true;
        }

        // Find the largest uniform chart scale that fits every chart into the atlas.
        PackingRectangle?[]? placements;
        bool[]? rotated;
        float occupancy;
        double chartScale;
        var binarySearchAttempts = 0;

        if (MaxTextureSize == 0) // Preserve 1:1 texel density: grow the atlas edge until everything fits.
        {
            while (!TryPack(1.0, atlasEdge, out placements, out rotated, out occupancy))
            {
                if (atlasEdge >= atlasEdgeCeiling)
                {
                    placements = null;
                    break;
                }

                Console.WriteLine(
                    $" -> [{DebugName}] WARNING: {charts.Count} texture charts do not fit in a " +
                    $"{atlasEdge}x{atlasEdge} atlas; retrying at {atlasEdge * 2}x{atlasEdge * 2}.");
                atlasEdge *= 2;
            }
            chartScale = 1.0;
        }
        else // binary search best scale
        {
            void FindCappedPacking()
            {
                if (TryPack(1.0, atlasEdge, out placements, out rotated, out occupancy))
                {
                    chartScale = 1.0;
                    return;
                }

                placements = null;
                rotated = null;
                occupancy = 0f;

                var low = 0.0; // largest scale known to fit
                var high = 1.0; // smallest scale known not to fit)

                for (int i = 0; i < ScaleSearchMaxIterations && high - low > ScaleSearchTolerance; i++)
                {
                    binarySearchAttempts++;
                    var mid = (low + high) / 2;
                    if (TryPack(mid, atlasEdge, out var attemptPlacements, out var attemptRotated,
                            out var attemptOccupancy))
                    {
                        low = mid;
                        placements = attemptPlacements;
                        rotated = attemptRotated;
                        occupancy = attemptOccupancy;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                chartScale = low;
            }

            FindCappedPacking();

            // If its smallest viable chart scale cannot be packed, retry this atlas at progressively larger sizes
            // but never past the configured cap.
            while (placements == null && atlasEdge < atlasEdgeCeiling)
            {
                int previousEdge = atlasEdge;
                atlasEdge = Math.Min(atlasEdge * 2, atlasEdgeCeiling);

                Console.WriteLine(
                    $" -> [{DebugName}] WARNING: {charts.Count} texture charts do not fit in a " +
                    $"{previousEdge}x{previousEdge} atlas; retrying this atlas at " +
                    $"{atlasEdge}x{atlasEdge} (atlas ceiling {atlasEdgeCeiling}, " +
                    $"configured --max-texture-size: {MaxTextureSize}).");

                FindCappedPacking();
            }
        }

        if (placements == null || rotated == null)
        {
            throw new InvalidOperationException(
                $"{DebugName}: cannot fit {charts.Count} texture charts into one atlas within the " +
                $"{atlasEdgeCeiling}x{atlasEdgeCeiling} limit" +
                (MaxTextureSize > 0 ? $" imposed by --max-texture-size ({MaxTextureSize})" : string.Empty) +
                ". Increase --max-texture-size or --divisions so each part carries fewer charts.");
        }

        for (var i = 0; i < charts.Count; i++)
        {
            charts[i].PackedRectangle = placements[i]!;
            charts[i].Rotated = rotated[i];

            if (charts[i].NaturalWidth * chartScale < 1.0 || charts[i].NaturalHeight * chartScale < 1.0)
            {
                charts[i].EffectivePadding = 0;
            }
            else
            {
                charts[i].EffectivePadding = charts[i].Padding;
            }
        }

        // Report atlas statistics and warn about charts that ended up smaller than one pixel.
        var achievedDensity = maxSourceDensity * targetDensity * chartScale;
        Console.WriteLine(
            $" -> [{DebugName}] Single atlas: {atlasEdge}x{atlasEdge}px, {charts.Count} charts, " +
            $"occupancy {occupancy * 100:F1}%, texel density {achievedDensity:F1} px/uv" +
            (chartScale < 1.0
                ? $" (target {targetDensity:F1} px/uv, scale {chartScale:F3}, {binarySearchAttempts} bin-search attempts)"
                : string.Empty));

        var subPixelCharts = 0;
        string? worstChart = null;
        var worstChartSize = double.MaxValue;
        foreach (var chart in charts)
        {
            var chartWidth = chart.NaturalWidth * chartScale;
            var chartHeight = chart.NaturalHeight * chartScale;
            if (chartWidth >= 1.0 && chartHeight >= 1.0)
                continue;

            subPixelCharts++;
            var minDimension = Math.Min(chartWidth, chartHeight);
            if (minDimension < worstChartSize)
            {
                worstChartSize = minDimension;
                worstChart = $"{chart.Material.Name} ({chartWidth:F2}x{chartHeight:F2} px)";
            }
        }

        if (subPixelCharts > 0)
        {
            Console.WriteLine(
                $" -> [{DebugName}] WARNING: {subPixelCharts} of {charts.Count} charts are smaller than one " +
                $"pixel in the atlas (worst: {worstChart}); texture detail is lost for these charts. " +
                "Increase --max-texture-size or set it to 0.");
        }

        // Create the atlas images.
        using var baseColorAtlas = new Image<Rgba32>(atlasEdge, atlasEdge);
        using var normalAtlas = hasNormalMap
            ? new Image<Rgba32>(atlasEdge, atlasEdge, new Rgba32(128, 128, 255, 255))
            : null;
        var newTextureVertices = new Dictionary<Vertex2, int>(_textureVertices.Count);

        foreach (var chart in charts)
        {
            int padding = chart.EffectivePadding;
            int scaledWidth = (chart.Rotated ? chart.PackedRectangle.Height : chart.PackedRectangle.Width) -
                              2 * padding;
            int scaledHeight = (chart.Rotated ? chart.PackedRectangle.Width : chart.PackedRectangle.Height) -
                               2 * padding;

            using (var block = BuildBaseColorBlock(chart, padding, scaledWidth, scaledHeight))
            {
                if (chart.Rotated) block.Mutate(context => context.Rotate(RotateMode.Rotate90));

                baseColorAtlas.Mutate(context =>
                    context.DrawImage(block,
                        new Point(chart.PackedRectangle.X, chart.PackedRectangle.Y), 1f));
            }

            if (normalAtlas != null)
            {
                using var block = BuildNormalBlock(chart, padding, scaledWidth, scaledHeight);
                if (chart.Rotated) block.Mutate(context => context.Rotate(RotateMode.Rotate90));

                normalAtlas.Mutate(context =>
                    context.DrawImage(block,
                        new Point(chart.PackedRectangle.X, chart.PackedRectangle.Y), 1f));
            }

            // Rewrite UV coords
            var u0 = chart.UvBounds.Left;
            var v0 = chart.UvBounds.Top;
            var uRange = Math.Max(chart.UvBounds.Width, float.Epsilon);
            var vRange = Math.Max(chart.UvBounds.Height, float.Epsilon);

            double atlasU0, atlasV0, atlasWidth, atlasHeight;
            if (chart.Rotated)
            {
                // The chart block was rotated 90° clockwise: chart U runs top-to-bottom along the
                // packed rect's vertical axis, chart V runs left-to-right along its horizontal axis.
                atlasU0 = (chart.PackedRectangle.X + padding) / (double)atlasEdge;
                atlasV0 = (atlasEdge - (chart.PackedRectangle.Y + padding)) / (double)atlasEdge;
                atlasWidth = scaledHeight / (double)atlasEdge;
                atlasHeight = scaledWidth / (double)atlasEdge;
            }
            else
            {
                atlasU0 = (chart.PackedRectangle.X + padding) / (double)atlasEdge;
                atlasV0 = (atlasEdge - (chart.PackedRectangle.Y + padding + scaledHeight)) /
                          (double)atlasEdge;
                atlasWidth = scaledWidth / (double)atlasEdge;
                atlasHeight = scaledHeight / (double)atlasEdge;
            }

            Vertex2 MapUv(Vertex2 uv)
            {
                var relativeU = (uv.X - u0) / uRange;
                var relativeV = (uv.Y - v0) / vRange;
                return chart.Rotated
                    ? new Vertex2(
                        Math.Clamp(atlasU0 + relativeV * atlasWidth, 0, 1),
                        Math.Clamp(atlasV0 - relativeU * atlasHeight, 0, 1))
                    : new Vertex2(
                        Math.Clamp(atlasU0 + relativeU * atlasWidth, 0, 1),
                        Math.Clamp(atlasV0 + relativeV * atlasHeight, 0, 1));
            }

            foreach (var faceIndex in chart.FaceIndexes)
            {
                var face = _faces[faceIndex];
                face.TextureIndexA = newTextureVertices.AddIndex(MapUv(_textureVertices[face.TextureIndexA]));
                face.TextureIndexB = newTextureVertices.AddIndex(MapUv(_textureVertices[face.TextureIndexB]));
                face.TextureIndexC = newTextureVertices.AddIndex(MapUv(_textureVertices[face.TextureIndexC]));
                face.MaterialIndex = 0;
            }
        }

        _textureVertices = newTextureVertices.OrderBy(item => item.Value).Select(item => item.Key).ToList();

        // Save as image
        var anyPngSource = usedMaterials.Any(m => m.Texture != null &&
            Path.GetExtension(m.Texture).Equals(".png", StringComparison.OrdinalIgnoreCase));
        var extension = SingleAtlasExtension(TexturesStrategy, TextureFormat, anyPngSource);
        var baseColorFileName = $"{Name}-texture-diffuse{extension}";
        SaveSingleAtlas(baseColorAtlas, Path.Combine(targetFolder, baseColorFileName));

        string? normalFileName = null;
        if (normalAtlas != null)
        {
            normalFileName = $"{Name}-texture-normal.png";
            normalAtlas.SaveAsPng(Path.Combine(targetFolder, normalFileName));
        }

        var representative = usedMaterials[0];
        _materials =
        [
            new Material(
                $"{Name}-material",
                baseColorFileName,
                normalFileName,
                ambientColor: new RGB(1, 1, 1),
                diffuseColor: new RGB(1, 1, 1),
                specularColor: representative.SpecularColor,
                specularExponent: representative.SpecularExponent,
                dissolve: 1.0,
                illuminationModel: representative.IlluminationModel)
        ];
    }

    /// <summary>
    /// Builds a padded base-color tile for <paramref name="chart"/>.
    /// </summary>
    /// <remarks>Returns a solid white block when no diffuse texture exists.</remarks>
    private Image<Rgba32> BuildBaseColorBlock(SingleAtlasChart chart, int padding, int scaledWidth, int scaledHeight)
    {
        if (chart.DiffuseTexture == null)
        {
            return new Image<Rgba32>(
                scaledWidth + 2 * padding,
                scaledHeight + 2 * padding,
                new Rgba32(255, 255, 255, 255));
        }

        var sourceRectangle = GetSourceRectangle(
            chart.UvBounds, chart.DiffuseTexture.Width, chart.DiffuseTexture.Height);
        return BuildPaddedBlock(chart.DiffuseTexture, sourceRectangle, padding, scaledWidth, scaledHeight);
    }

    /// <summary>
    /// Builds a padded normal-map tile for <paramref name="chart"/>.
    /// </summary>
    /// <remarks>Returns a flat tangent-space normal block when no normal map exists.</remarks>
    private static Image<Rgba32> BuildNormalBlock(SingleAtlasChart chart, int padding, int scaledWidth,
        int scaledHeight)
    {
        if (chart.NormalTexture == null)
        {
            return new Image<Rgba32>(
                scaledWidth + 2 * padding,
                scaledHeight + 2 * padding,
                new Rgba32(128, 128, 255, 255));
        }

        var sourceRectangle = GetSourceRectangle(
            chart.UvBounds, chart.NormalTexture.Width, chart.NormalTexture.Height);

        return BuildPaddedBlock(chart.NormalTexture, sourceRectangle, padding, scaledWidth, scaledHeight);
    }

    /// <summary>
    /// Converts a UV sub-rectangle (UDIM-aware) to the corresponding pixel rectangle inside a single source texture.
    /// flips Y to ImageSharp's top-left origin.
    /// </summary>
    private static Rectangle GetSourceRectangle(RectangleF uvBounds, int textureWidth, int textureHeight)
    {
        var u0 = uvBounds.Left;
        var v0 = uvBounds.Top;
        var u1 = u0 + uvBounds.Width;
        var v1 = v0 + uvBounds.Height;

        var tileU = (int)Math.Floor(u0 + 1e-4);
        var tileV = (int)Math.Floor(v0 + 1e-4);

        var u0Fraction = Math.Clamp(u0 - tileU, 0.0, 1.0);
        var v0Fraction = Math.Clamp(v0 - tileV, 0.0, 1.0);
        var u1Fraction = Math.Clamp(u1 - tileU, 0.0, 1.0);
        var v1Fraction = Math.Clamp(v1 - tileV, 0.0, 1.0);

        var startX = Math.Clamp((int)Math.Floor(u0Fraction * textureWidth + 0.5), 0, textureWidth - 1);
        var endX = Math.Clamp((int)Math.Ceiling(u1Fraction * textureWidth - 0.5), 1, textureWidth);
        var startBottom = Math.Clamp((int)Math.Floor(v0Fraction * textureHeight + 0.5), 0, textureHeight - 1);
        var endBottom = Math.Clamp((int)Math.Ceiling(v1Fraction * textureHeight - 0.5), 1, textureHeight);

        var width = Math.Max(1, endX - startX);
        var height = Math.Max(1, endBottom - startBottom);
        var startY = Math.Clamp(textureHeight - endBottom, 0, textureHeight - height);

        return new Rectangle(startX, startY, width, height);
    }

    /// <summary>
    /// Picks the file extension for the single-atlas output.
    /// </summary>
    private static string SingleAtlasExtension(TexturesStrategy strategy, TextureFormat format, bool anyPngSource) =>
        format == TextureFormat.Webp
        ? ".webp"
        : (strategy == TexturesStrategy.Compress
            ? ".jpg"
            : (anyPngSource ? ".png" : ".jpg"));

    /// <summary>
    /// Saves a single-atlas image using the encoder implied by <paramref name="path"/>'s extension.
    /// </summary>
    private void SaveSingleAtlas(Image image, string path)
    {
        if (TextureFormat == TextureFormat.Webp)
        {
            image.SaveAsWebp(path, new WebpEncoder
            {
                FileFormat = WebpFileFormatType.Lossy,
                Quality = Math.Clamp(TextureQuality, 1, 100)
            });
        }
        else if (Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            image.SaveAsPng(path);
        }
        else
        {
            image.SaveAsJpeg(path, CreateEncoder());
        }
    }

    private void LoadTexturesCache()
    {
        Parallel.ForEach(_materials, material =>
        {
            if (!string.IsNullOrEmpty(material.Texture))
                TexturesCache.GetTexture(material.Texture);
            if (!string.IsNullOrEmpty(material.NormalMap))
                TexturesCache.GetTexture(material.NormalMap);
        });
    }

    private JpegEncoder CreateEncoder() => new JpegEncoder { Quality = Math.Clamp(TextureQuality, 1, 100) };

    private static readonly string[] JpegExtensions = { ".jpg", ".jpeg" };

    /// <summary>
    /// Output file extension for a repacked atlas, honoring the selected texture format. Normal maps
    /// are always PNG regardless of format/strategy - see <see cref="SaveAtlas"/>.
    /// </summary>
    private string AtlasExtension(string sourcePath, bool isNormalMap)
        => isNormalMap ? ".png"
           : TextureFormat == TextureFormat.Webp ? ".webp"
           : (TexturesStrategy == TexturesStrategy.Repack ? Path.GetExtension(sourcePath) : ".jpg");

    /// <summary>
    /// Saves a repacked atlas with the encoder matching the current strategy and format.
    /// WebP is always encoded lossy at TextureQuality; for the classic formats RepackCompressed is
    /// always lossy JPEG at TextureQuality, and Repack re-encodes at TextureQuality only when the
    /// output stays JPEG (matching the source), otherwise it's a lossless save in the original
    /// format. Normal maps are always saved losslessly (PNG), ignoring TextureFormat/TexturesStrategy:
    /// JPEG/WebP lossy compression corrupts the directional data encoded in the RGB channels,
    /// producing visible lighting artifacts even though the diffuse texture tolerates it fine.
    /// </summary>
    private void SaveAtlas(Image image, string path, bool isNormalMap)
    {
        if (isNormalMap)
            image.SaveAsPng(path);
        else if (TextureFormat == TextureFormat.Webp)
        {
            // Always lossy: lossless WebP of an already-lossy source (e.g. a JPEG source atlas) can be
            // larger than the source and defeat the purpose. Lossy WebP at TextureQuality is smaller
            // than both PNG and JPEG at comparable quality.
            image.SaveAsWebp(path, new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = Math.Clamp(TextureQuality, 1, 100) });
        }
        else if (TexturesStrategy == TexturesStrategy.Repack)
        {
            // Repack keeps the source format (see AtlasExtension); when that format is JPEG, re-encode
            // at TextureQuality (e.g. --fine-texture-quality for LOD-0) instead of ImageSharp's default
            // quality so the setting actually controls Repack-strategy atlases, not just RepackCompressed.
            if (JpegExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                image.SaveAsJpeg(path, CreateEncoder());
            else
                image.Save(path);
        }
        else
            image.SaveAsJpeg(path, CreateEncoder());
    }

    /// <summary>
    /// Downscales an image in place so that neither side exceeds MaxTextureSize (when set), after
    /// applying TextureDownscale. Used by the Compress strategy (e.g. the tileset root tile) so its
    /// textures are not stored at full source resolution.
    /// </summary>
    private void ApplyTextureSizeLimit(Image image)
    {
        var s = Math.Clamp(TextureDownscale, float.Epsilon, 1.0f);
        if (MaxTextureSize > 0)
        {
            int maxDim = Math.Max(image.Width, image.Height);
            if (maxDim * s > MaxTextureSize)
                s = Math.Clamp(MaxTextureSize / (float)maxDim, float.Epsilon, 1.0f);
        }
        if (s < 1.0f)
        {
            int w = Math.Max(1, (int)(image.Width * s));
            int h = Math.Max(1, (int)(image.Height * s));
            image.Mutate(x => x.Resize(w, h));
        }
    }

    private void BinPackTextures(string targetFolder, int materialIndex, IReadOnlyList<List<int>> clusters,
        IDictionary<Vertex2, int> newTextureVertices, ICollection<Task> tasks)
    {
        var packSw = Stopwatch.StartNew();
        long nextProgressMs = 5000;

        var material = _materials[materialIndex];

        if (material.Texture == null && material.NormalMap == null) return;

        var texture = material.Texture != null ? TexturesCache.GetTexture(material.Texture) : null;
        var normalMap = material.NormalMap != null ? TexturesCache.GetTexture(material.NormalMap) : null;

        int textureWidth = material.Texture != null ? texture!.Width : normalMap!.Width;
        int textureHeight = material.Texture != null ? texture!.Height : normalMap!.Height;

        float scale = Math.Clamp(TextureDownscale, float.Epsilon, 1.0f);

        // Absolute cap: never repack an atlas from a source resolution larger than MaxTextureSize
        // per side. This bounds the dominant LOD-0 texture cost. 0 disables the cap.
        if (MaxTextureSize > 0)
        {
            int maxSrcDim = Math.Max(textureWidth, textureHeight);
            if (maxSrcDim * scale > MaxTextureSize)
                scale = Math.Clamp(MaxTextureSize / (float)maxSrcDim, float.Epsilon, 1.0f);
        }

        int effWidth  = Math.Max(1, (int)(textureWidth  * scale));
        int effHeight = Math.Max(1, (int)(textureHeight * scale));

        var clustersRects = clusters.Select(GetClusterRect).ToArray();

        CalculateMaxMinAreaRect(clustersRects, effWidth, effHeight, Padding, out var maxWidth, out var maxHeight,
            out var textureArea);

        var edgeLength = Math.Max(Common.NextPowerOfTwo((int)Math.Sqrt(textureArea)), 32);

        if (edgeLength < maxWidth)
            edgeLength = Common.NextPowerOfTwo((int)maxWidth);

        if (edgeLength < maxHeight)
            edgeLength = Common.NextPowerOfTwo((int)maxHeight);

        // NOTE: We could enable rotations but it would be a bit more complex
        var binPack = new MaxRectanglesBinPack(edgeLength, edgeLength, false);

        var newTexture = material.Texture != null ? new Image<Rgba32>(edgeLength, edgeLength) : null;
        var newNormalMap = material.NormalMap != null ? new Image<Rgba32>(edgeLength, edgeLength) : null;

        string? textureFileName = null, normalMapFileName = null, newPathTexture = null, newPathNormalMap = null;
        int count = 0;

        for (int i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];
            var clusterBoundary = clustersRects[i]; // [0..1] UV box

            // Absolute bounds from cluster
            double u0 = clusterBoundary.Left;
            double v0 = clusterBoundary.Top;
            double u1 = u0 + clusterBoundary.Width;
            double v1 = v0 + clusterBoundary.Height;

            // ---- UDIM tile localization ----
            // Determine which UDIM tile this cluster belongs to.
            int tileU = (int)Math.Floor(u0 + 1e-4);
            int tileV = (int)Math.Floor(v0 + 1e-4);

            // If a cluster spans multiple tiles, consider splitting by tile;
            // for now we just clamp to this tile (assert/log to catch it).
            if (Math.Floor(u1 - 1e-4) != tileU || Math.Floor(v1 - 1e-4) != tileV)
            {
                Debug.WriteLine($"[UDIM] Cluster spans multiple tiles: U[{u0},{u1}] V[{v0},{v1}]");
            }

            // Fractional (tile-local) UVs in [0,1]
            double u0f = Math.Clamp(u0 - tileU, 0.0, 1.0);
            double v0f = Math.Clamp(v0 - tileV, 0.0, 1.0);
            double u1f = Math.Clamp(u1 - tileU, 0.0, 1.0);
            double v1f = Math.Clamp(v1 - tileV, 0.0, 1.0);

            // ---- Pixel-center crop in the source tile ----
            int sx = Math.Clamp((int)Math.Floor(u0f * textureWidth + 0.5), 0, textureWidth - 1);
            int ex = Math.Clamp((int)Math.Ceiling(u1f * textureWidth - 0.5), 1, textureWidth);
            int sb = Math.Clamp((int)Math.Floor(v0f * textureHeight + 0.5), 0, textureHeight - 1);
            int eb = Math.Clamp((int)Math.Ceiling(v1f * textureHeight - 0.5), 1, textureHeight);

            int sw = Math.Max(1, ex - sx);
            int sh = Math.Max(1, eb - sb);

            // ImageSharp uses top-left origin; OBJ UVs are bottom-left → convert Y
            int syTL = Math.Clamp(textureHeight - eb, 0, textureHeight - sh);
            var srcRect = new Rectangle(sx, syTL, sw, sh);

            // Atlas-space dimensions (may be smaller than source when TextureDownscale < 1)
            int scaledSw = Math.Max(1, (int)Math.Round(sw * scale));
            int scaledSh = Math.Max(1, (int)Math.Round(sh * scale));

            // ---------- reserve atlas space WITH padding ----------
            var packRect = binPack.Insert(scaledSw + 2 * Padding, scaledSh + 2 * Padding,
                                          FreeRectangleChoiceHeuristic.RectangleBestAreaFit);

            // If we ran out of room: save current atlas, start a new one (keeps your behavior)
            if (packRect.Width == 0)
            {
                textureFileName = material.Texture != null
                    ? $"{Name}-texture-diffuse-{materialIndex}-{material.Name}{AtlasExtension(material.Texture, false)}" : null;
                normalMapFileName = material.NormalMap != null
                    ? $"{Name}-texture-normal-{materialIndex}-{material.Name}{AtlasExtension(material.NormalMap, true)}" : null;

                if (material.Texture != null) {
                    newPathTexture = Path.Combine(targetFolder, textureFileName!);
                    SaveAtlas(newTexture!, newPathTexture, false); newTexture!.Dispose();
                }

                if (material.NormalMap != null) {
                    newPathNormalMap = Path.Combine(targetFolder, normalMapFileName!);
                    SaveAtlas(newNormalMap!, newPathNormalMap, true);
                    newNormalMap!.Dispose();
                }

                // fresh atlas
                newTexture = material.Texture != null ? new Image<Rgba32>(edgeLength, edgeLength) : null;
                newNormalMap = material.NormalMap != null ? new Image<Rgba32>(edgeLength, edgeLength) : null;
                binPack = new MaxRectanglesBinPack(edgeLength, edgeLength, false);
                material.Texture = textureFileName;
                material.NormalMap = normalMapFileName;

                // avoid name collision, clone material
                count++;
                material = new Material(material.Name + "-" + count, textureFileName, normalMapFileName,
                    material.AmbientColor, material.DiffuseColor, material.SpecularColor,
                    material.SpecularExponent, material.Dissolve, material.IlluminationModel);
                _materials.Add(material);
                materialIndex = _materials.Count - 1;

                // try again
                packRect = binPack.Insert(scaledSw + 2 * Padding, scaledSh + 2 * Padding,
                                          FreeRectangleChoiceHeuristic.RectangleBestAreaFit);
                if (packRect.Width == 0)
                    throw new Exception($"Packing failed for {scaledSw}x{scaledSh} into {edgeLength}x{edgeLength} (occ {binPack.Occupancy()})");
            }

            int destInnerX = packRect.X + Padding;
            int destInnerY = packRect.Y + Padding;
            int destOuterX = destInnerX - Padding;
            int destOuterY = destInnerY - Padding;


            if (material.Texture != null)
            {
                using var block = BuildPaddedBlock(texture!, srcRect, Padding, scaledSw, scaledSh);
                newTexture!.Mutate(c => c.DrawImage(block, new Point(destOuterX, destOuterY), 1f));
            }
            if (material.NormalMap != null)
            {
                using var blockN = BuildPaddedBlock(normalMap!, srcRect, Padding, scaledSw, scaledSh);
                newNormalMap!.Mutate(c => c.DrawImage(blockN, new Point(destOuterX, destOuterY), 1f));
            }

            double atlasU0 = destInnerX / (double)edgeLength;
            double atlasV0 = (edgeLength - (destInnerY + scaledSh)) / (double)edgeLength;
            double innerUw = scaledSw / (double)edgeLength;
            double innerVh = scaledSh / (double)edgeLength;

            Vertex2 MapUV(double rx, double ry) =>
                new((float)Math.Clamp(atlasU0 + rx * innerUw, 0, 1),
                    (float)Math.Clamp(atlasV0 + ry * innerVh, 0, 1));

            for (int idx = 0; idx < cluster.Count; idx++)
            {
                var faceIndex = cluster[idx];
                var face = _faces[faceIndex];

                var vtA = _textureVertices[face.TextureIndexA];
                var vtB = _textureVertices[face.TextureIndexB];
                var vtC = _textureVertices[face.TextureIndexC];

                // chart-local [0..1] (avoid mixing full-texture scales)
                double rxA = (vtA.X - u0) / Math.Max(u1 - u0, double.Epsilon);
                double ryA = (vtA.Y - v0) / Math.Max(v1 - v0, double.Epsilon);
                double rxB = (vtB.X - u0) / Math.Max(u1 - u0, double.Epsilon);
                double ryB = (vtB.Y - v0) / Math.Max(v1 - v0, double.Epsilon);
                double rxC = (vtC.X - u0) / Math.Max(u1 - u0, double.Epsilon);
                double ryC = (vtC.Y - v0) / Math.Max(v1 - v0, double.Epsilon);

                var newVtA = MapUV(rxA, ryA);
                var newVtB = MapUV(rxB, ryB);
                var newVtC = MapUV(rxC, ryC);

                var newIndexVtA = newTextureVertices.AddIndex(newVtA);
                var newIndexVtB = newTextureVertices.AddIndex(newVtB);
                var newIndexVtC = newTextureVertices.AddIndex(newVtC);

                face.TextureIndexA = newIndexVtA;
                face.TextureIndexB = newIndexVtB;
                face.TextureIndexC = newIndexVtC;
                face.MaterialIndex = materialIndex;
            }

            if (packSw.ElapsedMilliseconds >= nextProgressMs)
            {
                Console.WriteLine($" -> [{DebugName}] Repacking texture '{_materials[materialIndex].Name}': {i + 1}/{clusters.Count} ({(i + 1) * 100 / clusters.Count}%) clusters ({packSw.Elapsed.TotalSeconds:F0}s)...");
                nextProgressMs += 5000;
            }
        }

        // ---------- saving ----------
        if (material.Texture != null)
        {
            textureFileName = $"{Name}-texture-diffuse-{materialIndex}-{material.Name}{AtlasExtension(material.Texture, false)}";
            newPathTexture = Path.Combine(targetFolder, textureFileName);
        }

        if (material.NormalMap != null)
        {
            normalMapFileName = $"{Name}-texture-normal-{materialIndex}-{material.Name}{AtlasExtension(material.NormalMap, true)}";
            newPathNormalMap = Path.Combine(targetFolder, normalMapFileName);
        }

        var saveTaskTexture = new Task(t =>
        {
            var tx = (Image<Rgba32>)t!;
            SaveAtlas(tx, newPathTexture!, false);
            tx.Dispose();
        }, newTexture, TaskCreationOptions.LongRunning);

        var saveTaskNormalMap = new Task(t =>
        {
            var tx = (Image<Rgba32>)t!;
            SaveAtlas(tx, newPathNormalMap!, true);
            tx.Dispose();
        }, newNormalMap, TaskCreationOptions.LongRunning);

        if (material.Texture != null) {
            tasks.Add(saveTaskTexture);
            saveTaskTexture.Start();
            material.Texture = textureFileName;

        }

        if (material.NormalMap != null) {
            tasks.Add(saveTaskNormalMap);
            saveTaskNormalMap.Start();
            material.NormalMap = normalMapFileName;
        }
    }

    // Adds bleed padding to each chart when estimating total area and max dims.
    private void CalculateMaxMinAreaRect(
        RectangleF[] clustersRects,
        int textureWidth,
        int textureHeight,
        int paddingPx,                        // <-- NEW
        out double maxWidth,                  // pixels (already padded)
        out double maxHeight,                 // pixels (already padded)
        out double textureArea)               // pixels^2 (sum of padded chart areas)
    {
        long areaPx = 0;
        int maxW = 0;
        int maxH = 0;

        for (int index = 0; index < clustersRects.Length; index++)
        {
            var rect = clustersRects[index];

            // Chart size in pixels from UV fraction
            int w = Math.Max(1, (int)Math.Ceiling(rect.Width * textureWidth));
            int h = Math.Max(1, (int)Math.Ceiling(rect.Height * textureHeight));

            // Add padding on both sides
            int wPad = Math.Max(1, w + 2 * paddingPx);
            int hPad = Math.Max(1, h + 2 * paddingPx);

            areaPx += (long)wPad * (long)hPad;
            if (wPad > maxW) maxW = wPad;
            if (hPad > maxH) maxH = hPad;
        }

        maxWidth = maxW;          // already in pixels (no further multiply)
        maxHeight = maxH;         // already in pixels (no further multiply)
        textureArea = areaPx;     // in pixels^2
    }


    /// <summary>
    /// Calculates the bounding box of a set of points.
    /// </summary>
    /// <param name="cluster"></param>
    /// <returns></returns>
    private RectangleF GetClusterRect(IReadOnlyList<int> cluster)
    {
        double maxX = double.MinValue, maxY = double.MinValue;
        double minX = double.MaxValue, minY = double.MaxValue;

        for (var n = 0; n < cluster.Count; n++)
        {
            var face = _faces[cluster[n]];

            var vtA = _textureVertices[face.TextureIndexA];
            var vtB = _textureVertices[face.TextureIndexB];
            var vtC = _textureVertices[face.TextureIndexC];

            maxX = Math.Max(Math.Max(Math.Max(maxX, vtC.X), vtB.X), vtA.X);
            maxY = Math.Max(Math.Max(Math.Max(maxY, vtC.Y), vtB.Y), vtA.Y);

            minX = Math.Min(Math.Min(Math.Min(minX, vtC.X), vtB.X), vtA.X);
            minY = Math.Min(Math.Min(Math.Min(minY, vtC.Y), vtB.Y), vtA.Y);
        }

        return new RectangleF((float)minX, (float)minY, (float)(maxX - minX), (float)(maxY - minY));
    }

    private double GetTextureArea(IReadOnlyList<int> facesIndexes)
    {
        double area = 0;

        for (var index = 0; index < facesIndexes.Count; index++)
        {
            var faceIndex = facesIndexes[index];

            var vtA = _textureVertices[_faces[faceIndex].TextureIndexA];
            var vtB = _textureVertices[_faces[faceIndex].TextureIndexB];
            var vtC = _textureVertices[_faces[faceIndex].TextureIndexC];

            area += Common.Area(vtA, vtB, vtC);
        }

        return area;
    }

    private static List<List<int>> GetFacesClusters(IEnumerable<int> facesIndexes,
        IReadOnlyDictionary<int, List<int>> facesMapper)
    {

        var clusters = new List<List<int>>();
        var remainingFacesIndexes = new HashSet<int>(facesIndexes);

        var first = remainingFacesIndexes.First();
        var currentCluster = new List<int> { first };
        var currentClusterCache = new HashSet<int> { first };
        remainingFacesIndexes.Remove(first);

        var lastRemainingFacesCount = remainingFacesIndexes.Count;

        while (remainingFacesIndexes.Count > 0)
        {
            var cnt = currentCluster.Count;

            for (var index = 0; index < currentCluster.Count; index++)
            {
                var faceIndex = currentCluster[index];

                if (!facesMapper.TryGetValue(faceIndex, out var connectedFaces))
                    continue;

                for (var i = 0; i < connectedFaces.Count; i++)
                {
                    var connectedFace = connectedFaces[i];
                    if (currentClusterCache.Contains(connectedFace)) continue;

                    currentCluster.Add(connectedFace);
                    currentClusterCache.Add(connectedFace);
                    remainingFacesIndexes.Remove(connectedFace);
                }
            }

            // No new face was added
            if (cnt == currentCluster.Count)
            {
                // Add the cluster
                clusters.Add(currentCluster);

                // If no more faces, exit
                if (remainingFacesIndexes.Count == 0) break;

                // Let's continue with the next cluster
                var next = remainingFacesIndexes.First();
                currentCluster = [next];
                currentClusterCache = [next];
                remainingFacesIndexes.Remove(next);
            }

            if (lastRemainingFacesCount == remainingFacesIndexes.Count)
            {
                Debug.WriteLine("Discarding " + remainingFacesIndexes.Count + " faces.");
                break;
            }

            lastRemainingFacesCount = remainingFacesIndexes.Count;
        }

        // Add the cluster
        clusters.Add(currentCluster);
        return clusters;
    }

    // Two faces are only in the same UV island if they share a real 3D edge (position-index
    // match - true mesh adjacency) AND the UV mapping is continuous across it (matching
    // texture-index edge - no seam). Position edges are the map key; each occurrence also
    // carries its paired texture edge so GetFacesMapper can check that second condition.
    // Requiring only the texture edge (as this used to) lets two faces that merely reuse the
    // same UV coordinates - e.g. two unrelated rooms whose floors intentionally share one
    // tileable UV layout, deduplicated by the exporter into the same vt indices - be wrongly
    // treated as one contiguous island, even though they don't share a single 3D vertex.
    private static Dictionary<int, List<int>> GetFacesMapper(Dictionary<Edge, List<(int FaceIndex, Edge TextureEdge)>> edgesMapper)
    {
        var facesMapper = new Dictionary<int, List<int>>();

        foreach (var edge in edgesMapper)
        {
            var entries = edge.Value;
            for (var i = 0; i < entries.Count; i++)
            {
                var (faceIndex, textureEdge) = entries[i];
                if (!facesMapper.ContainsKey(faceIndex))
                    facesMapper.Add(faceIndex, []);

                for (var index = 0; index < entries.Count; index++)
                {
                    var (f, otherTextureEdge) = entries[index];
                    if (f != faceIndex && textureEdge.Equals(otherTextureEdge))
                        facesMapper[faceIndex].Add(f);
                }
            }
        }

        return facesMapper;
    }

    private Dictionary<Edge, List<(int FaceIndex, Edge TextureEdge)>> GetEdgesMapper(IReadOnlyList<int> facesIndexes,
        int[] canonicalIndex)
    {
        var edgesMapper = new Dictionary<Edge, List<(int, Edge)>>();
        edgesMapper.EnsureCapacity(facesIndexes.Count * 3);

        void AddEdge(int posA, int posB, int texA, int texB, int faceIndex)
        {
            var posEdge = new Edge(canonicalIndex[posA], canonicalIndex[posB]);

            if (!edgesMapper.TryGetValue(posEdge, out var list))
            {
                list = [];
                edgesMapper.Add(posEdge, list);
            }

            list.Add((faceIndex, new Edge(texA, texB)));
        }

        for (var idx = 0; idx < facesIndexes.Count; idx++)
        {
            var faceIndex = facesIndexes[idx];
            var f = _faces[faceIndex];

            AddEdge(f.IndexA, f.IndexB, f.TextureIndexA, f.TextureIndexB, faceIndex);
            AddEdge(f.IndexB, f.IndexC, f.TextureIndexB, f.TextureIndexC, faceIndex);
            AddEdge(f.IndexA, f.IndexC, f.TextureIndexA, f.TextureIndexC, faceIndex);
        }

        return edgesMapper;
    }

    private List<List<int>> GetFacesByMaterial()
    {
        var res = _materials.Select(_ => new List<int>()).ToList();

        for (var i = 0; i < _faces.Count; i++)
        {
            var f = _faces[i];

            res[f.MaterialIndex].Add(i);
        }

        return res;
    }

    #region Utils

    public Box3 Bounds
    {
        get
        {
            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var minZ = double.MaxValue;

            var maxX = double.MinValue;
            var maxY = double.MinValue;
            var maxZ = double.MinValue;

            for (var index = 0; index < _vertices.Count; index++)
            {
                var v = _vertices[index];
                minX = minX < v.X ? minX : v.X;
                minY = minY < v.Y ? minY : v.Y;
                minZ = minZ < v.Z ? minZ : v.Z;

                maxX = v.X > maxX ? v.X : maxX;
                maxY = v.Y > maxY ? v.Y : maxY;
                maxZ = v.Z > maxZ ? v.Z : maxZ;
            }

            return new Box3(minX, minY, minZ, maxX, maxY, maxZ);
        }
    }

    public double AverageEdgeLength
    {
        get
        {
            if (_faces.Count == 0) return 0;

            var total = 0.0;

            for (var index = 0; index < _faces.Count; index++)
            {
                var f = _faces[index];
                var a = _vertices[f.IndexA];
                var b = _vertices[f.IndexB];
                var c = _vertices[f.IndexC];

                total += a.Distance(b) + b.Distance(c) + c.Distance(a);
            }

            return total / (_faces.Count * 3);
        }
    }

    public double MaximumEdgeLength
    {
        get
        {
            if (_faces.Count == 0) return 0;

            var max = 0.0;

            for (var index = 0; index < _faces.Count; index++)
            {
                var f = _faces[index];
                var a = _vertices[f.IndexA];
                var b = _vertices[f.IndexB];
                var c = _vertices[f.IndexC];

                var ab = a.Distance(b);
                var bc = b.Distance(c);
                var ca = c.Distance(a);

                if (ab > max) max = ab;
                if (bc > max) max = bc;
                if (ca > max) max = ca;
            }

            return max;
        }
    }

    public Vertex3 GetAverageOrientation()
    {
        double x = 0;
        double y = 0;
        double z = 0;

        for (var index = 0; index < _faces.Count; index++)
        {
            var f = _faces[index];
            var v1 = _vertices[f.IndexA];
            var v2 = _vertices[f.IndexB];
            var v3 = _vertices[f.IndexC];

            var orientation = Common.Orientation(v1, v2, v3);

            x += orientation.X;
            y += orientation.Y;
            z += orientation.Z;
        }

        x /= _faces.Count;
        y /= _faces.Count;
        z /= _faces.Count;

        // Calculate x, y and z angles
        var xAngle = Math.Atan2(y, z);
        var yAngle = Math.Atan2(x, z);
        var zAngle = Math.Atan2(y, x);

        return new Vertex3(xAngle, yAngle, zAngle);
    }

    public Vertex3 GetVertexBaricenter()
    {
        var x = 0.0;
        var y = 0.0;
        var z = 0.0;

        for (var index = 0; index < _vertices.Count; index++)
        {
            var v = _vertices[index];
            x += v.X;
            y += v.Y;
            z += v.Z;
        }

        x /= _vertices.Count;
        y /= _vertices.Count;
        z /= _vertices.Count;

        return new Vertex3(x, y, z);
    }

    public Vertex3 GetVertexMedian()
    {
        var count = _vertices.Count;
        if (count == 0)
            return new Vertex3(0, 0, 0);

        var xs = new double[count];
        var ys = new double[count];
        var zs = new double[count];

        for (var i = 0; i < count; i++)
        {
            xs[i] = _vertices[i].X;
            ys[i] = _vertices[i].Y;
            zs[i] = _vertices[i].Z;
        }

        Array.Sort(xs);
        Array.Sort(ys);
        Array.Sort(zs);

        var mid = count / 2;
        return new Vertex3(xs[mid], ys[mid], zs[mid]);
    }

    public void WriteObj(string path, bool removeUnused = true)
    {
        var hasTextures = _materials.Count > 0 && _textureVertices.Count > 0;
        //Console.WriteLine($" -> '{Name}': {(hasTextures ? $"{_materials.Count} mat(s), {_textureVertices.Count} UVs [{TexturesStrategy}]" : "no textures")}");
        if (!hasTextures)
            _WriteObjWithoutTexture(path, removeUnused);
        else
            _WriteObjWithTexture(path, removeUnused);
    }

    private void RemoveUnusedVertices()
    {
        var newVertexes = new Dictionary<Vertex3, int>(_vertices.Count);
        var newColors = _vertexColors != null ? new List<RGB>(_vertices.Count) : null;

        for (var f = 0; f < _faces.Count; f++)
        {
            var face = _faces[f];

            var vA = _vertices[face.IndexA];
            var vB = _vertices[face.IndexB];
            var vC = _vertices[face.IndexC];

            if (!newVertexes.TryGetValue(vA, out var newVA))
            {
                newVA = newVertexes.AddIndex(vA);
                if (newColors != null)
                    newColors.Add(_vertexColors![face.IndexA]);
            }

            face.IndexA = newVA;

            if (!newVertexes.TryGetValue(vB, out var newVB))
            {
                newVB = newVertexes.AddIndex(vB);
                if (newColors != null)
                    newColors.Add(_vertexColors![face.IndexB]);
            }

            face.IndexB = newVB;

            if (!newVertexes.TryGetValue(vC, out var newVC))
            {
                newVC = newVertexes.AddIndex(vC);
                if (newColors != null)
                    newColors.Add(_vertexColors![face.IndexC]);
            }

            face.IndexC = newVC;
        }

        _vertices = newVertexes.Keys.ToList();
        _vertexColors = newColors;
    }

    private void RemoveUnusedVerticesAndUvs()
    {
        var newVertexes = new Dictionary<Vertex3, int>(_vertices.Count);
        var newUvs = new Dictionary<Vertex2, int>(_textureVertices.Count);
        var newMaterials = new Dictionary<Material, int>(_materials.Count);
        var newColors = _vertexColors != null ? new List<RGB>(_vertices.Count) : null;

        for (var f = 0; f < _faces.Count; f++)
        {
            var face = _faces[f];

            // Vertices

            var vA = _vertices[face.IndexA];
            var vB = _vertices[face.IndexB];
            var vC = _vertices[face.IndexC];

            if (!newVertexes.TryGetValue(vA, out var newVA))
            {
                newVA = newVertexes.AddIndex(vA);
                if (newColors != null)
                    newColors.Add(_vertexColors![face.IndexA]);
            }

            face.IndexA = newVA;

            if (!newVertexes.TryGetValue(vB, out var newVB))
            {
                newVB = newVertexes.AddIndex(vB);
                if (newColors != null)
                    newColors.Add(_vertexColors![face.IndexB]);
            }

            face.IndexB = newVB;

            if (!newVertexes.TryGetValue(vC, out var newVC))
            {
                newVC = newVertexes.AddIndex(vC);
                if (newColors != null)
                    newColors.Add(_vertexColors![face.IndexC]);
            }

            face.IndexC = newVC;

            // Texture vertices

            var uvA = _textureVertices[face.TextureIndexA];
            var uvB = _textureVertices[face.TextureIndexB];
            var uvC = _textureVertices[face.TextureIndexC];

            if (!newUvs.TryGetValue(uvA, out var newUvA))
                newUvA = newUvs.AddIndex(uvA);

            face.TextureIndexA = newUvA;

            if (!newUvs.TryGetValue(uvB, out var newUvB))
                newUvB = newUvs.AddIndex(uvB);

            face.TextureIndexB = newUvB;

            if (!newUvs.TryGetValue(uvC, out var newUvC))
                newUvC = newUvs.AddIndex(uvC);

            face.TextureIndexC = newUvC;

            // Materials

            var material = _materials[face.MaterialIndex];

            if (!newMaterials.TryGetValue(material, out var newMaterial))
                newMaterial = newMaterials.AddIndex(material);

            face.MaterialIndex = newMaterial;
        }

        _vertices = newVertexes.Keys.ToList();
        _textureVertices = newUvs.Keys.ToList();
        _materials = newMaterials.Keys.ToList();
        _vertexColors = newColors;
    }

    // Crop the source rect, resize to (scaledW x scaledH), then add a padding-wide bleed ring by
    // edge-pixel repetition. Resizing before padding ensures the interior occupies exactly
    // [padding, padding+scaledW) in the returned block regardless of the scale factor.
    private static Image<Rgba32> BuildPaddedBlock(Image<Rgba32> src, Rectangle srcRect, int padding, int scaledW, int scaledH)
    {
        int sx = Math.Clamp(srcRect.X, 0, Math.Max(0, src.Width - 1));
        int sy = Math.Clamp(srcRect.Y, 0, Math.Max(0, src.Height - 1));
        int sw = Math.Clamp(srcRect.Width, 1, src.Width - sx);
        int sh = Math.Clamp(srcRect.Height, 1, src.Height - sy);

        // Step 1: crop the interior at full source resolution.
        using var interior = new Image<Rgba32>(sw, sh);
        src.ProcessPixelRows(interior, (srcAcc, intAcc) =>
        {
            for (int y = 0; y < sh; y++)
            {
                var srcRow = srcAcc.GetRowSpan(sy + y);
                var intRow = intAcc.GetRowSpan(y);
                for (int x = 0; x < sw; x++)
                    intRow[x] = srcRow[sx + x];
            }
        });

        // Step 2: resize the interior to the target atlas dimensions (skipped when 1:1).
        if (scaledW != sw || scaledH != sh)
            interior.Mutate(ctx => ctx.Resize(scaledW, scaledH));

        // Step 3: add the bleed ring from the (now resized) interior edge pixels.
        var block = new Image<Rgba32>(scaledW + 2 * padding, scaledH + 2 * padding);
        interior.ProcessPixelRows(block, (intAcc, blockAcc) =>
        {
            for (int destY = 0; destY < blockAcc.Height; destY++)
            {
                int intY = Math.Clamp(destY - padding, 0, scaledH - 1);
                var intRow = intAcc.GetRowSpan(intY);
                var destRow = blockAcc.GetRowSpan(destY);
                for (int destX = 0; destX < blockAcc.Width; destX++)
                {
                    int intX = Math.Clamp(destX - padding, 0, scaledW - 1);
                    destRow[destX] = intRow[intX];
                }
            }
        });
        return block;
    }


    private void _WriteObjWithTexture(string path, bool removeUnused = true)
    {
        if (removeUnused)
            RemoveUnusedVerticesAndUvs();

        var materialsPath = Path.ChangeExtension(path, "mtl");

        var folderPath = Path.GetDirectoryName(path) ?? string.Empty;

        if (SingleMaterialPerPart)
            MergeMaterialsIntoSingleAtlas(folderPath);
        else if (TexturesStrategy == TexturesStrategy.Repack || TexturesStrategy == TexturesStrategy.RepackCompressed)
            TrimTextures(folderPath);
        using (var writer = new FormattingStreamWriter(path, CultureInfo.InvariantCulture))
        {
            writer.Write("o ");
            writer.WriteLine(string.IsNullOrWhiteSpace(Name) ? DefaultName : Name);

            writer.WriteLine("mtllib {0}", Path.GetFileName(materialsPath));

            for (var i = 0; i < _vertices.Count; i++)
            {
                var vertex = _vertices[i];
                writer.Write("v ");
                writer.Write(vertex.X);
                writer.Write(" ");
                writer.Write(vertex.Y);
                writer.Write(" ");
                writer.Write(vertex.Z);

                if (_vertexColors != null)
                {
                    var color = _vertexColors[i];
                    writer.Write(" ");
                    writer.Write(color.R);
                    writer.Write(" ");
                    writer.Write(color.G);
                    writer.Write(" ");
                    writer.Write(color.B);
                }

                writer.WriteLine();
            }

            foreach (var textureVertex in _textureVertices)
            {
                writer.Write("vt ");
                writer.Write(textureVertex.X);
                writer.Write(" ");
                writer.WriteLine(textureVertex.Y);
            }

            var materialFaces = from face in _faces
                                group face by face.MaterialIndex
                into g
                                select g;

            // NOTE: If there are groups of faces without materials, they must be placed at the beginning
            foreach (var grp in materialFaces.OrderBy(item => item.Key))
            {
                writer.WriteLine($"usemtl {_materials[grp.Key].Name}");

                foreach (var face in grp)
                    writer.WriteLine(face.ToObj());
            }
        }

        var mtlFilePath = Path.ChangeExtension(path, "mtl");

        using (var writer = new FormattingStreamWriter(mtlFilePath, CultureInfo.InvariantCulture))
        {
            for (var index = 0; index < _materials.Count; index++)
            {
                var material = _materials[index];

                if (material.Texture != null && !SingleMaterialPerPart)
                {
                    switch (TexturesStrategy)
                    {
                        case TexturesStrategy.KeepOriginal:
                            {
                                var folder = Path.GetDirectoryName(path);

                                var textureFileName =
                                    $"{Path.GetFileNameWithoutExtension(path)}-texture-{index}{Path.GetExtension(material.Texture)}";

                                var newTexturePath =
                                    folder != null ? Path.Combine(folder, textureFileName) : textureFileName;

                                if (!File.Exists(newTexturePath))
                                    File.Copy(material.Texture, newTexturePath, true);

                                material.Texture = textureFileName;
                                break;
                            }
                        case TexturesStrategy.Compress:
                            {
                                var folder = Path.GetDirectoryName(path);

                                var textureFileName =
                                    $"{Path.GetFileNameWithoutExtension(path)}-texture-{index}{(TextureFormat == TextureFormat.Webp ? ".webp" : ".jpg")}";

                                var newTexturePath =
                                    folder != null ? Path.Combine(folder, textureFileName) : textureFileName;

                                if (File.Exists(newTexturePath))

                                    File.Delete(newTexturePath);

                                Console.WriteLine($" -> Compressing texture '{material.Texture}'");

                                using (var image = Image.Load(material.Texture))
                                {
                                    ApplyTextureSizeLimit(image);
                                    if (TextureFormat == TextureFormat.Webp)
                                        image.SaveAsWebp(newTexturePath, new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = Math.Clamp(TextureQuality, 1, 100) });
                                    else
                                        image.SaveAsJpeg(newTexturePath, CreateEncoder());
                                }

                                material.Texture = textureFileName;
                                break;
                            }
                    }
                }

                if (material.NormalMap != null)
                {
                    switch (TexturesStrategy)
                    {
                        case TexturesStrategy.KeepOriginal:
                            {
                                var folder = Path.GetDirectoryName(path);

                                var normalMapFileName =
                                    $"{Path.GetFileNameWithoutExtension(path)}-normalmap-{index}{Path.GetExtension(material.NormalMap)}";

                                var newNormalMapPath =
                                    folder != null ? Path.Combine(folder, normalMapFileName) : normalMapFileName;

                                if (!File.Exists(newNormalMapPath))
                                    File.Copy(material.NormalMap, newNormalMapPath, true);

                                material.NormalMap = normalMapFileName;
                                break;
                            }
                        case TexturesStrategy.Compress:
                            {
                                // Normal maps stay lossless (PNG) even under the "compress" strategy:
                                // JPEG chroma subsampling corrupts the directional data encoded in
                                // the RGB channels, producing visible lighting artifacts.
                                var folder = Path.GetDirectoryName(path);

                                var normalMapFileName =
                                    $"{Path.GetFileNameWithoutExtension(path)}-normalmap-{index}.png";

                                var newNormalMapPath =
                                    folder != null ? Path.Combine(folder, normalMapFileName) : normalMapFileName;

                                if (File.Exists(newNormalMapPath))
                                    File.Delete(newNormalMapPath);

                                Console.WriteLine($" -> Copying normal map '{material.NormalMap}'");

                                using (var image = Image.Load(material.NormalMap))
                                {
                                    ApplyTextureSizeLimit(image);
                                    image.SaveAsPng(newNormalMapPath);
                                }

                                material.NormalMap = normalMapFileName;
                                break;
                            }
                    }
                }

                writer.WriteLine(material.ToMtl());
            }
        }
    }

    private void _WriteObjWithoutTexture(string path, bool removeUnused = true)
    {
        if (removeUnused)
            RemoveUnusedVertices();

        using var writer = new FormattingStreamWriter(path, CultureInfo.InvariantCulture);

        writer.Write("o ");
        writer.WriteLine(string.IsNullOrWhiteSpace(Name) ? DefaultName : Name);

        for (var index = 0; index < _vertices.Count; index++)
        {
            var vertex = _vertices[index];
            writer.Write("v ");
            writer.Write(vertex.X);
            writer.Write(" ");
            writer.Write(vertex.Y);
            writer.Write(" ");
            writer.Write(vertex.Z);

            if (_vertexColors != null)
            {
                var color = _vertexColors[index];
                writer.Write(" ");
                writer.Write(color.R);
                writer.Write(" ");
                writer.Write(color.G);
                writer.Write(" ");
                writer.Write(color.B);
            }

            writer.WriteLine();
        }

        for (var index = 0; index < _faces.Count; index++)
        {
            var face = _faces[index];
            writer.WriteLine(face.ToObj());
        }
    }

    public int FacesCount => _faces.Count;
    public int VertexCount => _vertices.Count;

    #endregion
}

public enum TexturesStrategy
{
    KeepOriginal,
    Compress,
    Repack,
    RepackCompressed
}

public enum TextureFormat
{
    Jpeg,
    Webp,
    Ktx2
}
