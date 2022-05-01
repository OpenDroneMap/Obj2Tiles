using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Obj2Tiles.Library.Materials;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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

            var aSide = utils.GetDimension(face.A) < q;
            var bSide = utils.GetDimension(face.B) < q;
            var cSide = utils.GetDimension(face.C) < q;

            if (aSide)
            {
                if (bSide)
                {
                    if (cSide)
                    {
                        // All on the left

                        var indexALeft = leftVertices.AddIndex(face.A);
                        var indexBLeft = leftVertices.AddIndex(face.B);
                        var indexCLeft = leftVertices.AddIndex(face.C);

                        var indexATextureLeft = leftTextureVertices!.AddIndex(face.TA);
                        var indexBTextureLeft = leftTextureVertices!.AddIndex(face.TB);
                        var indexCTextureLeft = leftTextureVertices!.AddIndex(face.TC);

                        leftFaces.Add(new FaceT<Vertex3>(indexALeft, indexBLeft, indexCLeft, face.A, face.B, face.C,
                            indexATextureLeft, indexBTextureLeft, indexCTextureLeft,
                            face.MaterialIndex, face.TA!, face.TB!, face.TC!));
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

                        var indexARight = rightVertices.AddIndex(face.A);
                        var indexBRight = rightVertices.AddIndex(face.B);
                        var indexCRight = rightVertices.AddIndex(face.C);

                        var indexATextureRight = rightTextureVertices!.AddIndex(face.TA);
                        var indexBTextureRight = rightTextureVertices!.AddIndex(face.TB);
                        var indexCTextureRight = rightTextureVertices!.AddIndex(face.TC);

                        rightFaces.Add(new FaceT<Vertex3>(indexARight, indexBRight, indexCRight, face.A, face.B, face.C,
                            indexATextureRight, indexBTextureRight, indexCTextureRight,
                            face.MaterialIndex, face.TA!, face.TB!, face.TC!));
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

            leftFaces.Add(new FaceT<Vertex3>(indexVLLeft, indexVR1Left, indexVR2Left, vL, vR1, vR2,
                indexTextureVLLeft, indexTextureVR1Left, indexTextureVR2Left, materialIndex,
                tVL, tVR1, tVR2));

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

        var lface = new FaceT<Vertex3>(indexVLLeft, indexT1Left, indexT2Left, vL, t1, t2,
            indexTextureVLLeft, indexTextureT1Left, indexTextureT2Left, materialIndex,
            tVL, t1t, t2t);
        leftFaces.Add(lface);

        var rface1 = new FaceT<Vertex3>(indexT1Right, indexVR1Right, indexVR2Right, t1, vR1, vR2,
            indexTextureT1Right, indexTextureVR1Right, indexTextureVR2Right, materialIndex,
            t1t, tVR1, tVR2);
        rightFaces.Add(rface1);

        var rface2 = new FaceT<Vertex3>(indexT1Right, indexVR2Right, indexT2Right, t1, vR2, t2,
            indexTextureT1Right, indexTextureVR2Right, indexTextureT2Right, materialIndex,
            t1t, tVR2, t2t);
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

            rightFaces.Add(new FaceT<Vertex3>(indexVRRight, indexVL1Right, indexVL2Right, vR, vL1, vL2,
                indexTextureVRRight, indexTextureVL1Right, indexTextureVL2Right, materialIndex,
                tVR, tVL1, tVL2));

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

        var rface = new FaceT<Vertex3>(indexVRRight, indexT1Right, indexT2Right, vR, t1, t2,
            indexTextureVRRight, indexTextureT1Right, indexTextureT2Right, materialIndex,
            tVR, t1t, t2t);
        rightFaces.Add(rface);

        var lface1 = new FaceT<Vertex3>(indexT2Left, indexVL1Left, indexVL2Left, t2, vL1, vL2,
            indexTextureT2Left, indexTextureVL1Left, indexTextureVL2Left, materialIndex,
            t2t, tVL1, tVL2);
        leftFaces.Add(lface1);

        var lface2 = new FaceT<Vertex3>(indexT2Left, indexT1Left, indexVL1Left, t2, t1, vL1,
            indexTextureT2Left, indexTextureT1Left, indexTextureVL1Left, materialIndex,
            t2t, t1t, tVL1);
        leftFaces.Add(lface2);
    }

    public void TrimTextures()
    {
        /*
        Debug.WriteLine("\nTrimming textures of " + Name);

        var facesMapper = from face in Faces
            group face by face.MaterialIndex into grp
            select new
            {
                MaterialIndex = grp.Key,
                Faces = grp.OrderBy(f => f.TextureArea()).ToList(),
            };

        var imgSizeMapper = new Dictionary<string, Vertex2>();

        using (var outImage = new Image<Rgba32>(8192, 8192))
        {

            int outImageArea = 0;
            
            foreach (var g in facesMapper)
            {
                var material = Materials[g.MaterialIndex];
                Debug.WriteLine("Working on material " + material.Name);

                using (var texture = Image.Load<Rgba32>(material.Texture))
                {

                    //outImageArea += area;
                    

                }

            }

        }
        */
        
        var facesMapper = from face in Faces
            group face by face.MaterialIndex
            into grp
            select new
            {
                MaterialIndex = grp.Key,
                Faces = grp.ToList()
            };

        /*
        var imgSizeMapper = new Dictionary<string, Vertex2>();
        
        foreach (var g in facesMapper)
        {
            var material = Materials[g.MaterialIndex];
            
            using (var img = Image.Load(material.Texture))
            {
                imgSizeMapper.Add(material.Name, new Vertex2(img.Width, img.Height));
            }

            Debug.WriteLine("Working on material " + material.Name);

            var clusters = new List<Tuple<Material, FaceT<Vertex3>[]>>();

            var materialFaces = g.Faces;

            var currentCluster = new List<FaceT<Vertex3>> { materialFaces.First() };
            materialFaces.RemoveAt(0);

            while (materialFaces.Count > 0)
            {
                var cnt = currentCluster.Count;

                // Scorriamo tutte le facce del cluster
                for (var index = 0; index < currentCluster.Count; index++)
                {
                    var face = currentCluster[index];

                    // Scorriamo tutte le facce rimanenti
                    for (var n = 0; n < materialFaces.Count; n++)
                    {
                        var f = materialFaces[n];
                        if (face.IsTextureAdjacent(f))
                        {
                            currentCluster.Add(f);
                            materialFaces.RemoveAt(n);
                        }
                    }
                }

                // Non ho aggiunto nessuna faccia
                if (cnt == currentCluster.Count)
                {
                    // Aggiungo il cluster
                    clusters.Add(new Tuple<Material, FaceT<Vertex3>[]>(material, currentCluster.ToArray()));

                    Debug.WriteLine("Added cluster with " + currentCluster.Count + " faces");
                    
                    // Andiamo avanti con il prossimo cluster
                    currentCluster = new List<FaceT<Vertex3>> { materialFaces.First() };
                    materialFaces.RemoveAt(0);
                }
            }

            clusters.Add(new Tuple<Material, FaceT<Vertex3>[]>(material, currentCluster.ToArray()));

            Debug.WriteLine($"Material {g.MaterialIndex} has {clusters.Count} clusters");

            for (var index = 0; index < clusters.Count; index++)
            {
                var cluster = clusters[index];
                Debug.WriteLine("Working on cluster " + index);

                double maxX = double.MinValue, maxY = double.MinValue;
                double minX = double.MaxValue, minY = double.MaxValue;

                //var boxes = new List<Box2>();

                foreach (var face in cluster.Item2)
                {
                    maxX = Math.Max(Math.Max(Math.Max(maxX, face.TC.X), face.TB.X), face.TA.X);
                    maxY = Math.Max(Math.Max(Math.Max(maxY, face.TC.Y), face.TB.Y), face.TA.Y);

                    minX = Math.Min(Math.Min(Math.Min(minX, face.TC.X), face.TB.X), face.TA.X);
                    minY = Math.Min(Math.Min(Math.Min(minY, face.TC.Y), face.TB.Y), face.TA.Y);
                }

                var relativeBox = new Box2(minX, minY, maxX, maxY);
                
                Debug.WriteLine("Relative Box: (" + minX + " " + minY + ") - (" + maxX + " " + maxY + ")");

                var imgSize = imgSizeMapper[cluster.Item1.Name];

                var box = new Box2(minX * imgSize.X, minY * imgSize.Y, maxX * imgSize.X, maxY * imgSize.Y);
                
                Debug.WriteLine("Box: (" + box.Min.X + " " + box.Min.Y + ") - (" + box.Max.X + " " + box.Max.Y + ")");

                //var size = Math.Sqrt(boxes.Sum(b => b.Area));
            }

            //throw new NotImplementedException();
            // Aggiungere alla lista dei cluster il materiale corrispondente
            // Unificare i materiali
            // Incasellare i cluster in rettangoli
            // Aggiungere i rettangoli dal più grande al più piccolo al nuovo file di texture
            // Aggiornare tutte le coordinate delle texture
        }*/
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

            foreach (var v in Vertices)
            {
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