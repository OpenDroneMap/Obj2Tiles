using System.Diagnostics;
using System.Globalization;
using Obj2Tiles.Library.Algos;
using Obj2Tiles.Library.Materials;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Path = System.IO.Path;

namespace Obj2Tiles.Library.Geometry;

public class MeshT : IMesh
{
    public readonly IReadOnlyList<Vertex3> Vertices;
    public readonly IReadOnlyList<Vertex2> TextureVertices;
    public readonly IReadOnlyList<FaceT<Vertex3>> Faces;
    public readonly IReadOnlyList<Material> Materials;

    public const string DefaultName = "Mesh";

    public string Name { get; set; } = DefaultName;

    public MeshT(IReadOnlyList<Vertex3> vertices, IReadOnlyList<Vertex2> textureVertices,
        IReadOnlyList<FaceT<Vertex3>> faces, IReadOnlyList<Material> materials)
    {
        Vertices = vertices;
        TextureVertices = textureVertices;
        Faces = faces;
        Materials = materials;
    }

    public int Split(IVertexUtils utils, double q, out IMesh left,
        out IMesh right)
    {
        var leftVertices = new Dictionary<Vertex3, int>(Vertices.Count);
        var rightVertices = new Dictionary<Vertex3, int>(Vertices.Count);

        var leftFaces = new List<FaceT<Vertex3>>(Faces.Count);
        var rightFaces = new List<FaceT<Vertex3>>(Faces.Count);

        var leftTextureVertices = new Dictionary<Vertex2, int>(TextureVertices.Count);
        var rightTextureVertices = new Dictionary<Vertex2, int>(TextureVertices.Count);

        var count = 0;

        for (var index = 0; index < Faces.Count; index++)
        {
            var face = Faces[index];

            var vA = Vertices[face.IndexA];
            var vB = Vertices[face.IndexB];
            var vC = Vertices[face.IndexC];
            
            var vtA = TextureVertices[face.TextureIndexA];
            var vtB = TextureVertices[face.TextureIndexB];
            var vtC = TextureVertices[face.TextureIndexC];
            
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

                        var indexATextureLeft = leftTextureVertices!.AddIndex(vtA);
                        var indexBTextureLeft = leftTextureVertices!.AddIndex(vtB);
                        var indexCTextureLeft = leftTextureVertices!.AddIndex(vtC);

                        leftFaces.Add(new FaceT<Vertex3>(indexALeft, indexBLeft, indexCLeft, 
                            indexATextureLeft, indexBTextureLeft, indexCTextureLeft,
                            face.MaterialIndex));
                    }
                    else
                    {
                        IntersectRight2DWithTexture(utils, q, face.IndexC, face.IndexA, face.IndexB,
                            leftVertices,
                            rightVertices,
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
                            face.TextureIndexC, face.TextureIndexA, face.TextureIndexB,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        // All on the right

                        var indexARight = rightVertices.AddIndex(vA);
                        var indexBRight = rightVertices.AddIndex(vB);
                        var indexCRight = rightVertices.AddIndex(vC);

                        var indexATextureRight = rightTextureVertices!.AddIndex(vtA);
                        var indexBTextureRight = rightTextureVertices!.AddIndex(vtB);
                        var indexCTextureRight = rightTextureVertices!.AddIndex(vtC);

                        rightFaces.Add(new FaceT<Vertex3>(indexARight, indexBRight, indexCRight, 
                            indexATextureRight, indexBTextureRight, indexCTextureRight,
                            face.MaterialIndex));
                    }
                }
            }
        }

        var orderedLeftVertices = leftVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();
        var orderedRightVertices = rightVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();
        var rightMaterials = Materials.Select(mat => (Material)mat.Clone()).ToList();

        var orderedLeftTextureVertices = leftTextureVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();
        var orderedRightTextureVertices = rightTextureVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();
        var leftMaterials = Materials.Select(mat => (Material)mat.Clone()).ToList();

        left = new MeshT(orderedLeftVertices, orderedLeftTextureVertices, leftFaces, leftMaterials)
        {
            Name = $"{Name}-{utils.Axis}L"
        };
        right = new MeshT(orderedRightVertices, orderedRightTextureVertices, rightFaces, rightMaterials)
        {
            Name = $"{Name}-{utils.Axis}R"
        };

        return count;
    }

    private void IntersectLeft2DWithTexture(IVertexUtils utils, double q, int indexVL,
        int indexVR1, int indexVR2,
        IDictionary<Vertex3, int> leftVertices, IDictionary<Vertex3, int> rightVertices,
        int indexTextureVL, int indexTextureVR1, int indexTextureVR2,
        IDictionary<Vertex2, int> leftTextureVertices, IDictionary<Vertex2, int> rightTextureVertices,
        int materialIndex, ICollection<FaceT<Vertex3>> leftFaces, ICollection<FaceT<Vertex3>> rightFaces)
    {
        var vL = Vertices[indexVL];
        var vR1 = Vertices[indexVR1];
        var vR2 = Vertices[indexVR2];

        var tVL = TextureVertices[indexTextureVL];
        var tVR1 = TextureVertices[indexTextureVR1];
        var tVR2 = TextureVertices[indexTextureVR2];

        var indexVLLeft = leftVertices.AddIndex(vL);
        var indexTextureVLLeft = leftTextureVertices.AddIndex(tVL);

        if (Math.Abs(utils.GetDimension(vR1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vR2) - q) < Common.Epsilon)
        {
            // Right Vertices are on the line

            var indexVR1Left = leftVertices.AddIndex(vR1);
            var indexVR2Left = leftVertices.AddIndex(vR2);

            var indexTextureVR1Left = leftTextureVertices.AddIndex(tVR1);
            var indexTextureVR2Left = leftTextureVertices.AddIndex(tVR2);

            leftFaces.Add(new FaceT<Vertex3>(indexVLLeft, indexVR1Left, indexVR2Left, 
                indexTextureVLLeft, indexTextureVR1Left, indexTextureVR2Left, materialIndex));

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

        // Split texture
        var indexTextureVR1Right = rightTextureVertices.AddIndex(tVR1);
        var indexTextureVR2Right = rightTextureVertices.AddIndex(tVR2);

        var perc1 = GetIntersectionPerc(vL, vR1, t1);

        // Prima intersezione texture
        var t1t = tVL.CutEdgePerc(tVR1, perc1);
        var indexTextureT1Left = leftTextureVertices.AddIndex(t1t);
        var indexTextureT1Right = rightTextureVertices.AddIndex(t1t);

        var perc2 = GetIntersectionPerc(vL, vR2, t2);

        // Seconda intersezione texture
        var t2t = tVL.CutEdgePerc(tVR2, perc2);
        var indexTextureT2Left = leftTextureVertices.AddIndex(t2t);
        var indexTextureT2Right = rightTextureVertices.AddIndex(t2t);

        var lface = new FaceT<Vertex3>(indexVLLeft, indexT1Left, indexT2Left, 
            indexTextureVLLeft, indexTextureT1Left, indexTextureT2Left, materialIndex);
        leftFaces.Add(lface);

        var rface1 = new FaceT<Vertex3>(indexT1Right, indexVR1Right, indexVR2Right, 
            indexTextureT1Right, indexTextureVR1Right, indexTextureVR2Right, materialIndex);
        rightFaces.Add(rface1);

        var rface2 = new FaceT<Vertex3>(indexT1Right, indexVR2Right, indexT2Right, 
            indexTextureT1Right, indexTextureVR2Right, indexTextureT2Right, materialIndex);
        rightFaces.Add(rface2);
    }


    private void IntersectRight2DWithTexture(IVertexUtils utils, double q, int indexVR,
        int indexVL1, int indexVL2,
        IDictionary<Vertex3, int> leftVertices, IDictionary<Vertex3, int> rightVertices,
        int indexTextureVR, int indexTextureVL1, int indexTextureVL2,
        IDictionary<Vertex2, int> leftTextureVertices, IDictionary<Vertex2, int> rightTextureVertices,
        int materialIndex, ICollection<FaceT<Vertex3>> leftFaces, ICollection<FaceT<Vertex3>> rightFaces)
    {
        var vR = Vertices[indexVR];
        var vL1 = Vertices[indexVL1];
        var vL2 = Vertices[indexVL2];

        var tVR = TextureVertices[indexTextureVR];
        var tVL1 = TextureVertices[indexTextureVL1];
        var tVL2 = TextureVertices[indexTextureVL2];

        var indexVRRight = rightVertices.AddIndex(vR);
        var indexTextureVRRight = rightTextureVertices.AddIndex(tVR);

        if (Math.Abs(utils.GetDimension(vL1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vL2) - q) < Common.Epsilon)
        {
            // Left Vertices are on the line

            var indexVL1Right = rightVertices.AddIndex(vL1);
            var indexVL2Right = rightVertices.AddIndex(vL2);

            var indexTextureVL1Right = rightTextureVertices.AddIndex(tVL1);
            var indexTextureVL2Right = rightTextureVertices.AddIndex(tVL2);

            rightFaces.Add(new FaceT<Vertex3>(indexVRRight, indexVL1Right, indexVL2Right, 
                indexTextureVRRight, indexTextureVL1Right, indexTextureVL2Right, materialIndex));

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

        // Split texture
        var indexTextureVL1Left = leftTextureVertices.AddIndex(tVL1);
        var indexTextureVL2Left = leftTextureVertices.AddIndex(tVL2);

        var perc1 = GetIntersectionPerc(vR, vL1, t1);

        // Prima intersezione texture
        var t1t = tVR.CutEdgePerc(tVL1, perc1);
        var indexTextureT1Left = leftTextureVertices.AddIndex(t1t);
        var indexTextureT1Right = rightTextureVertices.AddIndex(t1t);

        var perc2 = GetIntersectionPerc(vR, vL2, t2);

        // Seconda intersezione texture
        var t2t = tVR.CutEdgePerc(tVL2, perc2);
        var indexTextureT2Left = leftTextureVertices.AddIndex(t2t);
        var indexTextureT2Right = rightTextureVertices.AddIndex(t2t);

        var rface = new FaceT<Vertex3>(indexVRRight, indexT1Right, indexT2Right, 
            indexTextureVRRight, indexTextureT1Right, indexTextureT2Right, materialIndex);
        rightFaces.Add(rface);

        var lface1 = new FaceT<Vertex3>(indexT2Left, indexVL1Left, indexVL2Left, 
            indexTextureT2Left, indexTextureVL1Left, indexTextureVL2Left, materialIndex);
        leftFaces.Add(lface1);

        var lface2 = new FaceT<Vertex3>(indexT2Left, indexT1Left, indexVL1Left, 
            indexTextureT2Left, indexTextureT1Left, indexTextureVL1Left, materialIndex);
        leftFaces.Add(lface2);
    }

    private class Edge : IEquatable<Edge>
    {
        public readonly int V1Index;

        public readonly int V2Index;
        //public readonly int FaceIndex;

        public Edge(int v1Index, int v2Index)
        {
            // Manteniamo un ordine
            if (v1Index > v2Index)
            {
                V1Index = v2Index;
                V2Index = v1Index;
            }
            else
            {
                V1Index = v1Index;
                V2Index = v2Index;
            }

            //FaceIndex = faceIndex;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Edge)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(V1Index, V2Index);
        }

        public bool Equals(Edge? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return V1Index == other.V1Index && V2Index == other.V2Index;
        }
    }

    public void TrimTextures()
    {
        Debug.WriteLine("\nTrimming textures of " + Name);

        var facesByMaterial = GetFacesByMaterial();

        for (var m = 0; m < facesByMaterial.Count; m++)
        {
            var material = Materials[m];
            var facesIndexes = facesByMaterial[m];
            Debug.WriteLine($"Working on material {m} -> {material.Name}");

            var sw = new Stopwatch();
            sw.Start();

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
            // Sort clusters by count
            clusters.Sort((a, b) => b.Count.CompareTo(a.Count));
            Debug.WriteLine("Done in " + sw.ElapsedMilliseconds + "ms");
            sw.Restart();

            Debug.WriteLine($"Material {material.Name} has {clusters.Count} clusters");

            using (var texture = Image.Load(material.Texture))
            {
                var textureWidth = texture.Width;
                var textureHeight = texture.Height;
                var area = textureWidth * textureHeight;
                
                var textureArea = GetTextureArea(facesIndexes) * area * 2;
                Debug.WriteLine("Texture area: " + textureArea);

                var edgeLength = NextPowerOfTwo((int)Math.Sqrt(textureArea));
                Debug.WriteLine("Edge length: " + edgeLength);

                // TODO: We could enable rotations but it would be a bit more complex
                var binPack = new MaxRectanglesBinPack(edgeLength, edgeLength, false);
                var rnd = new Random();

                using (var newTexture = new Image<Rgb24>(edgeLength, edgeLength))
                {
                    foreach (var cluster in clusters)
                    {
                        Debug.WriteLine("Processing cluster with " + cluster.Count + " faces");

                        var clusterBoundary = GetClusterRect(cluster);

                        Debug.WriteLine("Cluster boundary (percentage): " + clusterBoundary);

                        var x = (int)Math.Ceiling(clusterBoundary.Left * textureWidth);
                        var y = (int)Math.Ceiling(clusterBoundary.Top * textureHeight);
                        var w = (int)Math.Max(Math.Ceiling(clusterBoundary.Width * textureWidth), 1);
                        var h = (int)Math.Max(Math.Ceiling(clusterBoundary.Height * textureHeight), 1);

                        Debug.WriteLine($"Cluster boundary (pixel): ({x},{y}) size {w}x{h}");

                        var rect = binPack.Insert(w, h, FreeRectangleChoiceHeuristic.RectangleBestAreaFit);

                        if (rect.Width == 0)
                            throw new NotImplementedException("Need to enlarge texture, not implemented yet");
                        
                        Debug.WriteLine("Found place for cluster: " + rect);
                        
                        //var color = Color.FromRgb((byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255));
/*
                        newTexture.Mutate(x => x.FillPolygon(color, new(rect.X, rect.Y), 
                            new(rect.X + rect.Width, rect.Y), 
                            new(rect.X + rect.Width, rect.Y + rect.Height), 
                            new(rect.X, rect.Y + rect.Height)));
  */                      
                        Common.CopyImage(texture, newTexture, x, y, w, h, rect.X, rect.Y);

                        foreach (var faceIndex in cluster)
                        {
                            var face = Faces[faceIndex];
                            
                            //textureFaceA = new Vector2(rect.X / (float)textureWidth, rect.Y / (float)textureHeight);
                            
                        }
                        
                        /*
                        newTexture.Mutate(a =>
                        {
                            a.Clip(new RectangularPolygon(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height)),
                                context =>
                                {
                                    context.DrawImage(texture, new Point(rect.X, rect.Y), 1f);
                                });
                        });*/
                        
                        //CopyRect(texture, newTexture, x, y, w, h, rect);
                        
                        /*
                         * 1) Creo una maschera per la copia delle facce
                         * 2) Copio il cluster in una nuova texture
                         * 
                         */
                    }
                    
                    newTexture.Save(material.Name + ".png");
                }
            }

/*            foreach (var cluster in clusters)
            {
                Debug.WriteLine($"{cluster.Count} faces in cluster");;
            }

            for (var index = 0; index < clusters.Count; index++)
            {
                var cluster = clusters[index];
                Debug.WriteLine("Working on cluster " + index);

                double maxX = double.MinValue, maxY = double.MinValue;
                double minX = double.MaxValue, minY = double.MaxValue;

                //var boxes = new List<Box2>();

                foreach (var face in cluster.Item2)
                {
                    maxX = Math.Max(Math.Max(Math.Max(maxX, textureFaceC.X), textureFaceB.X), textureFaceA.X);
                    maxY = Math.Max(Math.Max(Math.Max(maxY, textureFaceC.Y), textureFaceB.Y), textureFaceA.Y);

                    minX = Math.Min(Math.Min(Math.Min(minX, textureFaceC.X), textureFaceB.X), textureFaceA.X);
                    minY = Math.Min(Math.Min(Math.Min(minY, textureFaceC.Y), textureFaceB.Y), textureFaceA.Y);
                }

                var relativeBox = new Box2(minX, minY, maxX, maxY);
                
                Debug.WriteLine("Relative Box: (" + minX + " " + minY + ") - (" + maxX + " " + maxY + ")");

                var imgSize = imgSizeMapper[cluster.Item1.Name];

                var box = new Box2(minX * imgSize.X, minY * imgSize.Y, maxX * imgSize.X, maxY * imgSize.Y);
                
                Debug.WriteLine("Box: (" + box.Min.X + " " + box.Min.Y + ") - (" + box.Max.X + " " + box.Max.Y + ")");

                //var size = Math.Sqrt(boxes.Sum(b => b.Area));
            }*/
        }
    }

    private RectangleF GetClusterRect(IReadOnlyList<int> cluster)
    {
        double maxX = double.MinValue, maxY = double.MinValue;
        double minX = double.MaxValue, minY = double.MaxValue;

        for (var n = 0; n < cluster.Count; n++)
        {
            var face = Faces[cluster[n]];

            var vtA = TextureVertices[face.TextureIndexA];
            var vtB = TextureVertices[face.TextureIndexB];
            var vtC = TextureVertices[face.TextureIndexC];
            
            maxX = Math.Max(Math.Max(Math.Max(maxX, vtC.X), vtB.X), vtA.X);
            maxY = Math.Max(Math.Max(Math.Max(maxY, vtC.Y), vtB.Y), vtA.Y);

            minX = Math.Min(Math.Min(Math.Min(minX, vtC.X), vtB.X), vtA.X);
            minY = Math.Min(Math.Min(Math.Min(minY, vtC.Y), vtB.Y), vtA.Y);
        }

        return new RectangleF((float)minX, (float)minY, (float)(maxX - minX), (float)(maxY - minY));
    }

    private double GetTextureArea(List<int> facesIndexes)
    {
        double area = 0;
        
        foreach (var faceIndex in facesIndexes)
        {
            var vtA = TextureVertices[Faces[faceIndex].TextureIndexA];
            var vtB = TextureVertices[Faces[faceIndex].TextureIndexB];
            var vtC = TextureVertices[Faces[faceIndex].TextureIndexC];
                
            area += Common.Area(vtA, vtB, vtC);
        }

        return area;
    }

    public static int NextPowerOfTwo(int x)
    {
        x--;
        x |= (x >> 1);
        x |= (x >> 2);
        x |= (x >> 4);
        x |= (x >> 8);
        x |= (x >> 16);
        return (x + 1);
    }

    private static List<List<int>> GetFacesClusters(IEnumerable<int> facesIndexes,
        IReadOnlyDictionary<int, List<int>> facesMapper)
    {
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

            // Non ho aggiunto nessuna faccia
            if (cnt == currentCluster.Count)
            {
                // Aggiungo il cluster
                clusters.Add(currentCluster);

                // Se non ce ne sono più possiamo uscire
                if (remainingFacesIndexes.Count == 0) break;

                // Andiamo avanti con il prossimo cluster
                currentCluster = new List<int> { remainingFacesIndexes.First() };
                remainingFacesIndexes.RemoveAt(0);
            }
        }

        // Aggiungo il cluster
        clusters.Add(currentCluster);
        return clusters;
    }

    private static Dictionary<int, List<int>> GetFacesMapper(Dictionary<Edge, List<int>> edgesMapper)
    {
        var facesMapper = new Dictionary<int, List<int>>();

        foreach (var edge in edgesMapper)
        {
            foreach (var faceIndex in edge.Value)
            {
                if (!facesMapper.ContainsKey(faceIndex))
                    facesMapper.Add(faceIndex, new List<int>());

                foreach (var f in edge.Value)
                {
                    if (f != faceIndex)
                        facesMapper[faceIndex].Add(f);
                }
            }
        }

        return facesMapper;
    }

    private Dictionary<Edge, List<int>> GetEdgesMapper(List<int> facesIndexes)
    {
        var edgesMapper = new Dictionary<Edge, List<int>>();
        edgesMapper.EnsureCapacity(facesIndexes.Count * 3);

        for (var idx = 0; idx < facesIndexes.Count; idx++)
        {
            var faceIndex = facesIndexes[idx];
            var f = Faces[faceIndex];
            var e1 = new Edge(f.TextureIndexA, f.TextureIndexB);
            var e2 = new Edge(f.TextureIndexB, f.TextureIndexC);
            var e3 = new Edge(f.TextureIndexA, f.TextureIndexC);

            if (!edgesMapper.ContainsKey(e1))
            {
                edgesMapper.Add(e1, new List<int>());
            }

            if (!edgesMapper.ContainsKey(e2))
            {
                edgesMapper.Add(e2, new List<int>());
            }

            if (!edgesMapper.ContainsKey(e3))
            {
                edgesMapper.Add(e3, new List<int>());
            }

            edgesMapper[e1].Add(faceIndex);
            edgesMapper[e2].Add(faceIndex);
            edgesMapper[e3].Add(faceIndex);
        }

        return edgesMapper;
    }

    private List<List<int>> GetFacesByMaterial()
    {
        var res = Materials.Select(m => new List<int>()).ToList();

        for (var i = 0; i < Faces.Count; i++)
        {
            var f = Faces[i];

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

            for (var index = 0; index < Vertices.Count; index++)
            {
                var v = Vertices[index];
                minX = minX < v.X ? minX : v.X;
                minY = minY < v.Y ? minY : v.Y;
                minZ = minZ < v.Z ? minZ : v.Z;

                maxX = v.X > maxX ? maxX : v.X;
                maxY = v.Y > maxY ? maxY : v.Y;
                maxZ = v.Z > maxZ ? maxZ : v.Z;
            }

            return new Box3(minX, minY, minZ, maxX, maxY, maxZ);
        }
    }

    public Vertex3 GetVertexBaricenter()
    {
        var x = 0.0;
        var y = 0.0;
        var z = 0.0;

        for (var index = 0; index < Vertices.Count; index++)
        {
            var v = Vertices[index];
            x += v.X;
            y += v.Y;
            z += v.Z;
        }

        x /= Vertices.Count;
        y /= Vertices.Count;
        z /= Vertices.Count;

        return new Vertex3(x, y, z);
    }

    /// <summary>
    /// Gets the distance of P from A (in percent) relative to segment AB
    /// </summary>
    /// <param name="a">Edge start</param>
    /// <param name="b">Edge end</param>
    /// <param name="p">Point on the segment</param>
    /// <returns></returns>
    private static double GetIntersectionPerc(Vertex3 a, Vertex3 b, Vertex3 p)
    {
        var edge1Length = a.Distance(b);
        var subEdge1Length = a.Distance(p);
        return subEdge1Length / edge1Length;
    }

    private static readonly CultureInfo en = CultureInfo.GetCultureInfo("en-US");

    public void WriteObj(string path)
    {
        var materialsPath = Path.ChangeExtension(path, "mtl");

        using (var writer = new StreamWriter(path))
        {
            if (TextureVertices.Any())
                writer.WriteLine("mtllib {0}", Path.GetFileName(materialsPath));

            writer.Write("o ");
            writer.WriteLine(string.IsNullOrWhiteSpace(Name) ? DefaultName : Name);

            CultureInfo.CurrentCulture = en;

            foreach (var vertex in Vertices)
            {
                writer.Write("v ");
                writer.Write(vertex.X);
                writer.Write(" ");
                writer.Write(vertex.Y);
                writer.Write(" ");
                writer.WriteLine(vertex.Z);
            }

            foreach (var textureVertex in TextureVertices)
            {
                writer.Write("vt ");
                writer.Write(textureVertex.X);
                writer.Write(" ");
                writer.WriteLine(textureVertex.Y);
            }

            var materialFaces = from face in Faces
                group face by face.MaterialIndex
                into g
                select g;

            // NOTE: Se ci sono gruppi di facce senza materiali vanno messe all'inizio
            foreach (var grp in materialFaces.OrderBy(item => item.Key))
            {
                writer.WriteLine($"usemtl {Materials[grp.Key].Name}");

                foreach (var face in grp)
                    writer.WriteLine(face.ToObj());
            }
        }

        if (Materials.Any())
        {
            var mtlFilePath = Path.ChangeExtension(path, "mtl");

            using var writer = new StreamWriter(mtlFilePath);
            for (var index = 0; index < Materials.Count; index++)
            {
                var material = Materials[index];
                if (material.Texture != null)
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

                writer.WriteLine(material.ToMtl());
            }
        }
    }

    public int FacesCount => Faces.Count;
    public int VertexCount => Vertices.Count;

    #endregion
}