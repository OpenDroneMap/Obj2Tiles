﻿using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Obj2Tiles.Library.Algos;
using Obj2Tiles.Library.Materials;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Path = System.IO.Path;

namespace Obj2Tiles.Library.Geometry;

public class MeshT : IMesh
{
    private List<Vertex3> _vertices;
    private readonly List<FaceB> _faces;
    private List<Material> _materials;

    public IReadOnlyList<Vertex3> Vertices => _vertices;
    public IReadOnlyList<FaceB> Faces => _faces;
    public IReadOnlyList<Material> Materials => _materials;

    public const string DefaultName = "Mesh";

    public string Name { get; set; } = DefaultName;

    public TexturesStrategy TexturesStrategy { get; set; }

    public MeshT(IEnumerable<Vertex3> vertices,
        IEnumerable<FaceB> faces, IEnumerable<Material> materials)
    {
        _vertices = new List<Vertex3>(vertices);
        _faces = new List<FaceB>(faces);
        _materials = new List<Material>(materials);
    }

    public int Split(IVertexUtils utils, double q, out IMesh left,
        out IMesh right)
    {
        var leftVertices = new Dictionary<Vertex3, int>(_vertices.Count);
        var rightVertices = new Dictionary<Vertex3, int>(_vertices.Count);

        var leftFaces = new List<FaceB>(_faces.Count);
        var rightFaces = new List<FaceB>(_faces.Count);


        var count = 0;

        for (var index = 0; index < _faces.Count; index++)
        {
            var face = _faces[index];

            var vA = _vertices[face.IndexA];
            var vB = _vertices[face.IndexB];
            var vC = _vertices[face.IndexC];

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

                        var indexALeft = leftVertices.AddIndex(vA);
                        var indexBLeft = leftVertices.AddIndex(vB);
                        var indexCLeft = leftVertices.AddIndex(vC);


                        leftFaces.Add(new FaceB(indexALeft, indexBLeft, indexCLeft,
                            face.MaterialIndex));
                    }
                    else
                    {
                        IntersectRight2DWithTexture(utils, q, face.IndexC, face.IndexA, face.IndexB,
                            leftVertices,
                            rightVertices,
                            face.MaterialIndex, leftFaces, rightFaces
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
                            face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2DWithTexture(utils, q, face.IndexA, face.IndexB, face.IndexC,
                            leftVertices,
                            rightVertices,
                            face.MaterialIndex, leftFaces,
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
                            face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2DWithTexture(utils, q, face.IndexB, face.IndexC, face.IndexA,
                            leftVertices,
                            rightVertices,
                            face.MaterialIndex, leftFaces,
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
                            face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        // All on the right

                        var indexARight = rightVertices.AddIndex(vA);
                        var indexBRight = rightVertices.AddIndex(vB);
                        var indexCRight = rightVertices.AddIndex(vC);


                        rightFaces.Add(new FaceB(indexARight, indexBRight, indexCRight,
                            face.MaterialIndex));
                    }
                }
            }
        }

        var orderedLeftVertices = leftVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var orderedRightVertices = rightVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var rightMaterials = _materials.Select(mat => (Material)mat.Clone());

        var leftMaterials = _materials.Select(mat => (Material)mat.Clone());

        left = new MeshT(orderedLeftVertices, leftFaces, leftMaterials)
        {
            Name = $"{Name}-{utils.Axis}L"
        };
        right = new MeshT(orderedRightVertices, rightFaces, rightMaterials)
        {
            Name = $"{Name}-{utils.Axis}R"
        };

        return count;
    }

    private void IntersectLeft2DWithTexture(IVertexUtils utils, double q, int indexVL,
        int indexVR1, int indexVR2,
        IDictionary<Vertex3, int> leftVertices, IDictionary<Vertex3, int> rightVertices,
        int materialIndex, ICollection<FaceB> leftFaces, ICollection<FaceB> rightFaces)
    {
        var vL = _vertices[indexVL];
        var vR1 = _vertices[indexVR1];
        var vR2 = _vertices[indexVR2];

        var indexVLLeft = leftVertices.AddIndex(vL);

        if (Math.Abs(utils.GetDimension(vR1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vR2) - q) < Common.Epsilon)
        {
            // Right Vertices are on the line

            var indexVR1Left = leftVertices.AddIndex(vR1);
            var indexVR2Left = leftVertices.AddIndex(vR2);


            leftFaces.Add(new FaceB(indexVLLeft, indexVR1Left, indexVR2Left, materialIndex));

            return;
        }

        var indexVR1Right = rightVertices.AddIndex(vR1);
        var indexVR2Right = rightVertices.AddIndex(vR2);

        // a on the left, b and c on the right

        // Prima intersezione
        var t1 = utils.CutEdge(vL, vR1, q);
        var indexT1Left = leftVertices.AddIndex(t1);
        var indexT1Right = rightVertices.AddIndex(t1);

        // Seconda intersezione
        var t2 = utils.CutEdge(vL, vR2, q);
        var indexT2Left = leftVertices.AddIndex(t2);
        var indexT2Right = rightVertices.AddIndex(t2);


        var perc1 = Common.GetIntersectionPerc(vL, vR1, t1);


        var perc2 = Common.GetIntersectionPerc(vL, vR2, t2);



        var lface = new FaceB(indexVLLeft, indexT1Left, indexT2Left, materialIndex);
        leftFaces.Add(lface);

        var rface1 = new FaceB(indexT1Right, indexVR1Right, indexVR2Right, materialIndex);
        rightFaces.Add(rface1);

        var rface2 = new FaceB(indexT1Right, indexVR2Right, indexT2Right, materialIndex);
        rightFaces.Add(rface2);
    }

    private void IntersectRight2DWithTexture(IVertexUtils utils, double q, int indexVR,
        int indexVL1, int indexVL2,
        IDictionary<Vertex3, int> leftVertices, IDictionary<Vertex3, int> rightVertices,
        int materialIndex, ICollection<FaceB> leftFaces, ICollection<FaceB> rightFaces)
    {
        var vR = _vertices[indexVR];
        var vL1 = _vertices[indexVL1];
        var vL2 = _vertices[indexVL2];


        var indexVRRight = rightVertices.AddIndex(vR);

        if (Math.Abs(utils.GetDimension(vL1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vL2) - q) < Common.Epsilon)
        {
            // Left Vertices are on the line

            var indexVL1Right = rightVertices.AddIndex(vL1);
            var indexVL2Right = rightVertices.AddIndex(vL2);


            rightFaces.Add(new FaceB(indexVRRight, indexVL1Right, indexVL2Right, materialIndex));

            return;
        }

        var indexVL1Left = leftVertices.AddIndex(vL1);
        var indexVL2Left = leftVertices.AddIndex(vL2);

        // a on the right, b and c on the left

        // Prima intersezione
        var t1 = utils.CutEdge(vR, vL1, q);
        var indexT1Left = leftVertices.AddIndex(t1);
        var indexT1Right = rightVertices.AddIndex(t1);

        // Seconda intersezione
        var t2 = utils.CutEdge(vR, vL2, q);
        var indexT2Left = leftVertices.AddIndex(t2);
        var indexT2Right = rightVertices.AddIndex(t2);


        var perc1 = Common.GetIntersectionPerc(vR, vL1, t1);



        var perc2 = Common.GetIntersectionPerc(vR, vL2, t2);



        var rface = new FaceB(indexVRRight, indexT1Right, indexT2Right, materialIndex);
        rightFaces.Add(rface);

        var lface1 = new FaceB(indexT2Left, indexVL1Left, indexVL2Left, materialIndex);
        leftFaces.Add(lface1);

        var lface2 = new FaceB(indexT2Left, indexT1Left, indexVL1Left, materialIndex);
        leftFaces.Add(lface2);
    }

    private void TrimTextures(string targetFolder)
    {
        Debug.WriteLine("Trimming textures of " + Name);

        var tasks = new List<Task>();

        LoadTexturesCache();

        var facesByMaterial = GetFacesByMaterial();


        var sw = new Stopwatch();

        for (var m = 0; m < facesByMaterial.Count; m++)
        {
            var material = _materials[m];
            var facesIndexes = facesByMaterial[m];
            Debug.WriteLine($"Working on material {m} -> {material.Name}");

            if (facesIndexes.Count == 0)
            {
                Debug.WriteLine("No faces with this material");
                continue;
            }

            sw.Restart();

            Debug.WriteLine("Creating edges mapper");
            var edgesMapper = GetEdgesMapper(facesIndexes);
            Debug.WriteLine("Done in " + sw.ElapsedMilliseconds + "ms");
            sw.Restart();

            Debug.WriteLine("Creating faces mapper");
            var facesMapper = GetFacesMapper(edgesMapper);
            Debug.WriteLine("Done in " + sw.ElapsedMilliseconds + "ms");
            sw.Restart();

            Debug.WriteLine("Assembling faces clusters");
            var clusters = GetFacesClusters(facesIndexes, facesMapper);
            Debug.WriteLine("Done in " + sw.ElapsedMilliseconds + "ms");
            sw.Restart();

            Debug.WriteLine("Sorting clusters");

            // Sort clusters by count (improves packing density, could be removed if we notice a bottleneck)
            clusters.Sort((a, b) => b.Count.CompareTo(a.Count));
            Debug.WriteLine("Done in " + sw.ElapsedMilliseconds + "ms");
            sw.Restart();

            Debug.WriteLine($"Material {material.Name} has {clusters.Count} clusters");

            Debug.WriteLine("Bin packing clusters");
            // BinPackTextures(targetFolder, m, clusters, tasks);
            Debug.WriteLine("Done in " + sw.ElapsedMilliseconds + "ms");
        }



        Debug.WriteLine("Waiting for save tasks to finish");
        sw.Restart();
        Task.WaitAll(tasks.ToArray());
        Debug.WriteLine("Done in " + sw.ElapsedMilliseconds + "ms");
    }

    private void LoadTexturesCache()
    {
        Parallel.ForEach(_materials, material =>
        {
            if (!string.IsNullOrEmpty(material.Texture))
                TexturesCache.GetTexture(material.Texture);
        });
    }

    private static readonly JpegEncoder encoder = new JpegEncoder { Quality = 75 };

    private void BinPackTextures(string targetFolder, int materialIndex, IReadOnlyList<List<int>> clusters, ICollection<Task> tasks)
    {
        var material = _materials[materialIndex];

        if (material.Texture == null)
            return;

        var texture = TexturesCache.GetTexture(material.Texture);

        var textureWidth = texture.Width;
        var textureHeight = texture.Height;
        var clustersRects = clusters.Select(GetClusterRect).ToArray();

        CalculateMaxMinAreaRect(clustersRects, textureWidth, textureHeight, out var maxWidth, out var maxHeight,
            out var textureArea);

        Debug.WriteLine("Texture area: " + textureArea);

        var edgeLength = Math.Max(Common.NextPowerOfTwo((int)Math.Sqrt(textureArea)), 32);

        if (edgeLength < maxWidth)
            edgeLength = Common.NextPowerOfTwo((int)maxWidth);

        if (edgeLength < maxHeight)
            edgeLength = Common.NextPowerOfTwo((int)maxHeight);

        Debug.WriteLine("Edge length: " + edgeLength);

        // NOTE: We could enable rotations but it would be a bit more complex
        var binPack = new MaxRectanglesBinPack(edgeLength, edgeLength, false);

        var newTexture = new Image<Rgba32>(edgeLength, edgeLength);

        string? textureFileName, newPath;
        var count = 0;

        for (var i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];
            Debug.WriteLine("Processing cluster with " + cluster.Count + " faces");

            var clusterBoundary = clustersRects[i];

            Debug.WriteLine("Cluster boundary (percentage): " + clusterBoundary);

            var clusterX = (int)Math.Floor(clusterBoundary.Left * textureWidth);
            var clusterY = (int)Math.Floor(clusterBoundary.Top * textureHeight);
            var clusterWidth = (int)Math.Max(Math.Ceiling(clusterBoundary.Width * textureWidth), 1);
            var clusterHeight = (int)Math.Max(Math.Ceiling(clusterBoundary.Height * textureHeight), 1);

            Debug.WriteLine(
                $"Cluster boundary (pixel): ({clusterX},{clusterY}) size {clusterWidth}x{clusterHeight}");

            var newTextureClusterRect = binPack.Insert(clusterWidth, clusterHeight,
                FreeRectangleChoiceHeuristic.RectangleBestAreaFit);

            if (newTextureClusterRect.Width == 0)
            {
                Debug.WriteLine("Somehow we could not pack everything in the texture, splitting it in two");

                textureFileName = $"{Name}-texture-{material.Name}{Path.GetExtension(material.Texture)}";
                newPath = Path.Combine(targetFolder, textureFileName);
                newTexture.Save(newPath);
                newTexture.Dispose();

                newTexture = new Image<Rgba32>(edgeLength, edgeLength);
                binPack = new MaxRectanglesBinPack(edgeLength, edgeLength, false);
                material.Texture = textureFileName;

                // Avoid texture name collision
                count++;

                material = new Material(material.Name + "-" + count, textureFileName, material.AmbientColor,
                    material.DiffuseColor,
                    material.SpecularColor, material.SpecularExponent, material.Dissolve, material.IlluminationModel);

                _materials.Add(material);
                materialIndex = _materials.Count - 1;

                // This is the second time we are here
                newTextureClusterRect = binPack.Insert(clusterWidth, clusterHeight,
                    FreeRectangleChoiceHeuristic.RectangleBestAreaFit);

                if (newTextureClusterRect.Width == 0)
                    throw new Exception(
                        $"Find room for cluster in a newly created texture, this is not supposed to happen. {clusterWidth}x{clusterHeight} in {edgeLength}x{edgeLength} with occupancy {binPack.Occupancy()}");
            }

            Debug.WriteLine("Found place for cluster at " + newTextureClusterRect);

            // Too long to explain this here, but it works
            var adjustedSourceY = Math.Max(texture.Height - (clusterY + clusterHeight), 0);
            var adjustedDestY = Math.Max(edgeLength - (newTextureClusterRect.Y + clusterHeight), 0);

            Common.CopyImage(texture, newTexture, clusterX, adjustedSourceY, clusterWidth, clusterHeight,
                newTextureClusterRect.X, adjustedDestY);

            var textureScaleX = (double)textureWidth / edgeLength;
            var textureScaleY = (double)textureHeight / edgeLength;

            Debug.WriteLine("Texture copied, now updating texture vertex coordinates");

            for (var index = 0; index < cluster.Count; index++)
            {
                var faceIndex = cluster[index];
                var face = _faces[faceIndex];



                // Cluster relative positions (percentage)
                var relativeClusterX = newTextureClusterRect.X / (double)edgeLength;
                var relativeClusterY = newTextureClusterRect.Y / (double)edgeLength;


                face.MaterialIndex = materialIndex;
            }
        }

        textureFileName = TexturesStrategy == TexturesStrategy.Repack
            ? $"{Name}-texture-{material.Name}{Path.GetExtension(material.Texture)}"
            : $"{Name}-texture-{material.Name}.jpg";

        newPath = Path.Combine(targetFolder, textureFileName);

        var saveTask = new Task(t =>
        {
            var tx = t as Image<Rgba32>;

            switch (TexturesStrategy)
            {
                case TexturesStrategy.RepackCompressed:
                    tx.SaveAsJpeg(newPath, encoder);
                    break;
                case TexturesStrategy.Repack:
                    tx.Save(newPath);
                    break;
                case TexturesStrategy.Compress:
                case TexturesStrategy.KeepOriginal:
                    throw new InvalidOperationException(
                        "KeepOriginal or Compress are meaningless here, we are repacking!");
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Debug.WriteLine("Saved texture to " + newPath);
            tx.Dispose();
        }, newTexture, TaskCreationOptions.LongRunning);

        tasks.Add(saveTask);
        saveTask.Start();

        material.Texture = textureFileName;
    }

    private void CalculateMaxMinAreaRect(RectangleF[] clustersRects, int textureWidth, int textureHeight,
        out double maxWidth, out double maxHeight, out double textureArea)
    {
        maxWidth = 0;
        maxHeight = 0;
        textureArea = 0;

        for (var index = 0; index < clustersRects.Length; index++)
        {
            var rect = clustersRects[index];

            textureArea += Math.Max(Math.Ceiling(rect.Width * textureWidth), 1) *
                           Math.Max(Math.Ceiling(rect.Height * textureHeight), 1);

            if (rect.Width > maxWidth)
            {
                maxWidth = rect.Width;
            }

            if (rect.Height > maxHeight)
            {
                maxHeight = rect.Height;
            }
        }

        maxWidth = Math.Ceiling(maxWidth * textureWidth);
        maxHeight = Math.Ceiling(maxHeight * textureHeight);
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

        }

        return new RectangleF((float)minX, (float)minY, (float)(maxX - minX), (float)(maxY - minY));
    }

    private double GetTextureArea(IReadOnlyList<int> facesIndexes)
    {
        double area = 0;

        for (var index = 0; index < facesIndexes.Count; index++)
        {
            var faceIndex = facesIndexes[index];
        }

        return area;
    }

    private static List<List<int>> GetFacesClusters(IEnumerable<int> facesIndexes,
        IReadOnlyDictionary<int, List<int>> facesMapper)
    {
        Debug.Assert(facesIndexes.Any(), "No faces in this cluster");

        var clusters = new List<List<int>>();
        var remainingFacesIndexes = new List<int>(facesIndexes);

        var currentCluster = new List<int> { remainingFacesIndexes.First() };
        remainingFacesIndexes.RemoveAt(0);

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
                    if (currentCluster.Contains(connectedFace)) continue;

                    currentCluster.Add(connectedFace);
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
                currentCluster = new List<int> { remainingFacesIndexes.First() };
                remainingFacesIndexes.RemoveAt(0);
            }
        }

        // Add the cluster
        clusters.Add(currentCluster);
        return clusters;
    }

    private static Dictionary<int, List<int>> GetFacesMapper(Dictionary<Edge, List<int>> edgesMapper)
    {
        var facesMapper = new Dictionary<int, List<int>>();

        foreach (var edge in edgesMapper)
        {
            for (var i = 0; i < edge.Value.Count; i++)
            {
                var faceIndex = edge.Value[i];
                if (!facesMapper.ContainsKey(faceIndex))
                    facesMapper.Add(faceIndex, new List<int>());

                for (var index = 0; index < edge.Value.Count; index++)
                {
                    var f = edge.Value[index];
                    if (f != faceIndex)
                        facesMapper[faceIndex].Add(f);
                }
            }
        }

        return facesMapper;
    }

    private Dictionary<Edge, List<int>> GetEdgesMapper(IReadOnlyList<int> facesIndexes)
    {
        var edgesMapper = new Dictionary<Edge, List<int>>();
        edgesMapper.EnsureCapacity(facesIndexes.Count * 3);

        for (var idx = 0; idx < facesIndexes.Count; idx++)
        {
            var faceIndex = facesIndexes[idx];
            var f = _faces[faceIndex];
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

    public void WriteObj(string path, bool removeUnused = true)
    {
        if (!_materials.Any())
            _WriteObjWithoutTexture(path, removeUnused);
        else
            _WriteObjWithTexture(path, removeUnused);
    }

    private void RemoveUnusedVertices()
    {
        var newVertexes = new Dictionary<Vertex3, int>(_vertices.Count);

        for (var f = 0; f < _faces.Count; f++)
        {
            var face = _faces[f];

            var vA = _vertices[face.IndexA];
            var vB = _vertices[face.IndexB];
            var vC = _vertices[face.IndexC];

            if (!newVertexes.TryGetValue(vA, out var newVA))
                newVA = newVertexes.AddIndex(vA);

            face.IndexA = newVA;

            if (!newVertexes.TryGetValue(vB, out var newVB))
                newVB = newVertexes.AddIndex(vB);

            face.IndexB = newVB;

            if (!newVertexes.TryGetValue(vC, out var newVC))
                newVC = newVertexes.AddIndex(vC);

            face.IndexC = newVC;
        }

        _vertices = newVertexes.Keys.ToList();
    }

    private void RemoveUnusedVerticesAndUvs()
    {
        var newVertexes = new Dictionary<Vertex3, int>(_vertices.Count);
        var newMaterials = new Dictionary<Material, int>(_materials.Count);

        for (var f = 0; f < _faces.Count; f++)
        {
            var face = _faces[f];

            // Vertices

            var vA = _vertices[face.IndexA];
            var vB = _vertices[face.IndexB];
            var vC = _vertices[face.IndexC];

            if (!newVertexes.TryGetValue(vA, out var newVA))
                newVA = newVertexes.AddIndex(vA);

            face.IndexA = newVA;

            if (!newVertexes.TryGetValue(vB, out var newVB))
                newVB = newVertexes.AddIndex(vB);

            face.IndexB = newVB;

            if (!newVertexes.TryGetValue(vC, out var newVC))
                newVC = newVertexes.AddIndex(vC);

            face.IndexC = newVC;

            // Materials

            var material = _materials[face.MaterialIndex];

            if (!newMaterials.TryGetValue(material, out var newMaterial))
                newMaterial = newMaterials.AddIndex(material);

            face.MaterialIndex = newMaterial;
        }

        _vertices = newVertexes.Keys.ToList();
        _materials = newMaterials.Keys.ToList();
    }


    private void _WriteObjWithTexture(string path, bool removeUnused = true)
    {
        if (removeUnused)
            RemoveUnusedVerticesAndUvs();

        var materialsPath = Path.ChangeExtension(path, "mtl");

        // if (TexturesStrategy == TexturesStrategy.Repack || TexturesStrategy == TexturesStrategy.RepackCompressed)
        //     TrimTextures(Path.GetDirectoryName(path));

        using (var writer = new FormattingStreamWriter(path, CultureInfo.InvariantCulture))
        {
            writer.Write("o ");
            writer.WriteLine(string.IsNullOrWhiteSpace(Name) ? DefaultName : Name);

            writer.WriteLine("mtllib {0}", Path.GetFileName(materialsPath));

            foreach (var vertex in _vertices)
            {
                writer.Write("v ");
                writer.Write(vertex.X);
                writer.Write(" ");
                writer.Write(vertex.Y);
                writer.Write(" ");
                writer.WriteLine(vertex.Z);
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

                if (material.Texture != null)
                {
                    if (TexturesStrategy == TexturesStrategy.KeepOriginal)
                    {
                        var folder = Path.GetDirectoryName(path);

                        var textureFileName =
                            $"{Path.GetFileNameWithoutExtension(path)}-texture-{index}{Path.GetExtension(material.Texture)}";

                        var newTexturePath =
                            folder != null ? Path.Combine(folder, textureFileName) : textureFileName;

                        if (!File.Exists(newTexturePath))
                            File.Copy(material.Texture, newTexturePath, true);

                        material.Texture = textureFileName;
                    }
                    else if (TexturesStrategy == TexturesStrategy.Compress)
                    {
                        var folder = Path.GetDirectoryName(path);

                        var textureFileName =
                            $"{Path.GetFileNameWithoutExtension(path)}-texture-{index}.jpg";

                        var newTexturePath =
                            folder != null ? Path.Combine(folder, textureFileName) : textureFileName;

                        if (File.Exists(newTexturePath))

                            File.Delete(newTexturePath);

                        Console.WriteLine($" -> Compressing texture '{material.Texture}'");

                        using (var image = Image.Load(material.Texture))
                        {
                            image.SaveAsJpeg(newTexturePath, encoder);
                        }

                        material.Texture = textureFileName;
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
            writer.WriteLine(vertex.Z);
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