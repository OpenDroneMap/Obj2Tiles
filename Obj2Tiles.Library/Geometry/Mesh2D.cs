using System.Globalization;
using Obj2Tiles.Library.Materials;

namespace Obj2Tiles.Library.Geometry;

public class Mesh2D
{
    public readonly IReadOnlyList<Vertex2D> Vertices;
    public readonly IReadOnlyList<Vertex2D> TextureVertices;
    public readonly IReadOnlyList<Face<Vertex2D>> Faces;
    public readonly IReadOnlyList<Material> Materials;

    public const string DefaultName = "Mesh";

    public string Name { get; set; } = DefaultName;

    public Box2D Bounds
    {
        get
        {
            var minX = Vertices.Min(v => v.x);
            var minY = Vertices.Min(v => v.y);
            var maxX = Vertices.Max(v => v.x);
            var maxY = Vertices.Max(v => v.y);
            
            return new Box2D(minX, minY, maxX, maxY);
        }
    }

    public Mesh2D(IReadOnlyList<Vertex2D> vertices, IReadOnlyList<Vertex2D> textureVertices,
        IReadOnlyList<Face<Vertex2D>> faces, IReadOnlyList<Material> materials)
    {
        Vertices = vertices;
        TextureVertices = textureVertices;
        Faces = faces;
        Materials = materials;
    }
    
    private int SplitWithTexture(IVertexUtils utils, double q, out Mesh2D left, out Mesh2D right)
    {
        var leftVertices = new Dictionary<Vertex2D, int>(Vertices.Count);
        var rightVertices = new Dictionary<Vertex2D, int>(Vertices.Count);

        var leftFaces = new List<Face<Vertex2D>>(Faces.Count);
        var rightFaces = new List<Face<Vertex2D>>(Faces.Count);

        var leftTextureVertices = new Dictionary<Vertex2D, int>(TextureVertices.Count);
        var rightTextureVertices = new Dictionary<Vertex2D, int>(TextureVertices.Count);

        var count = 0;

        foreach (var face in Faces)
        {
            //Console.WriteLine(face);

            var aSide = utils.GetDimension(face.A) < q;
            var bSide = utils.GetDimension(face.B) < q;
            var cSide = utils.GetDimension(face.C) < q;

            if (aSide)
            {
                if (bSide)
                {
                    if (cSide)
                    {
                        var indexALeft = leftVertices.AddIndex(face.A);
                        var indexBLeft = leftVertices.AddIndex(face.B);
                        var indexCLeft = leftVertices.AddIndex(face.C);

                        var indexATextureLeft = leftTextureVertices!.AddIndex(face.TA);
                        var indexBTextureLeft = leftTextureVertices!.AddIndex(face.TB);
                        var indexCTextureLeft = leftTextureVertices!.AddIndex(face.TC);

                        leftFaces.Add(new Face<Vertex2D>(indexALeft, indexBLeft, indexCLeft, face.A, face.B, face.C,
                            indexATextureLeft, indexBTextureLeft, indexCTextureLeft,
                            face.MaterialIndex!.Value, face.TA!, face.TB!, face.TC!));

                        //Console.WriteLine("All on the left");
                    }
                    else
                    {
                        IntersectRight2DWithTexture(utils, q, face.IndexC, face.IndexA, face.IndexB, leftVertices,
                            rightVertices,
                            face.TextureIndexC!.Value, face.TextureIndexA!.Value, face.TextureIndexB!.Value,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex!.Value, leftFaces, rightFaces
                        );
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectRight2DWithTexture(utils, q, face.IndexB, face.IndexC, face.IndexA, leftVertices,
                            rightVertices,
                            face.TextureIndexB!.Value, face.TextureIndexC!.Value, face.TextureIndexA!.Value,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex!.Value, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2DWithTexture(utils, q, face.IndexA, face.IndexB, face.IndexC, leftVertices,
                            rightVertices,
                            face.TextureIndexA!.Value, face.TextureIndexB!.Value, face.TextureIndexC!.Value,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex!.Value, leftFaces,
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
                        IntersectRight2DWithTexture(utils, q, face.IndexA, face.IndexB, face.IndexC, leftVertices,
                            rightVertices,
                            face.TextureIndexA!.Value, face.TextureIndexB!.Value, face.TextureIndexC!.Value,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex!.Value, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2DWithTexture(utils, q, face.IndexB, face.IndexC, face.IndexA, leftVertices,
                            rightVertices,
                            face.TextureIndexB!.Value, face.TextureIndexC!.Value, face.TextureIndexA!.Value,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex!.Value, leftFaces,
                            rightFaces);
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectLeft2DWithTexture(utils, q, face.IndexC, face.IndexA, face.IndexB, leftVertices,
                            rightVertices,
                            face.TextureIndexC!.Value, face.TextureIndexA!.Value, face.TextureIndexB!.Value,
                            leftTextureVertices, rightTextureVertices, face.MaterialIndex!.Value, leftFaces,
                            rightFaces);
                        count++;
                    }
                    else
                    {
                        var indexARight = rightVertices.AddIndex(face.A);
                        var indexBRight = rightVertices.AddIndex(face.B);
                        var indexCRight = rightVertices.AddIndex(face.C);

                        var indexATextureRight = rightTextureVertices!.AddIndex(face.TA);
                        var indexBTextureRight = rightTextureVertices!.AddIndex(face.TB);
                        var indexCTextureRight = rightTextureVertices!.AddIndex(face.TC);

                        rightFaces.Add(new Face<Vertex2D>(indexARight, indexBRight, indexCRight, face.A, face.B, face.C,
                            indexATextureRight, indexBTextureRight, indexCTextureRight,
                            face.MaterialIndex!.Value, face.TA!, face.TB!, face.TC!));

                        //Console.WriteLine("All on the right");
                    }
                }
            }
        }

        var orderedLeftVertices = leftVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();
        var orderedRightVertices = rightVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();

        var orderedLeftTextureVertices = leftTextureVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();
        var orderedRightTextureVertices = rightTextureVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();

        // Aggiungere split texture
        left = new Mesh2D(orderedLeftVertices, orderedLeftTextureVertices, leftFaces, Materials);
        right = new Mesh2D(orderedRightVertices, orderedRightTextureVertices, rightFaces, Materials);

        return count;
    }
    

    public int Split(IVertexUtils utils, double q, out Mesh2D left, out Mesh2D right)
    {
        //return SplitWithoutTexture(axis, q, out left, out right);
        return TextureVertices.Any()
            ? SplitWithTexture(utils, q, out left, out right)
            : SplitWithoutTexture(utils, q, out left, out right);
    }

    private void IntersectLeft2DWithTexture(IVertexUtils utils, double q, int indexVL, int indexVR1, int indexVR2,
        IDictionary<Vertex2D, int> leftVertices, IDictionary<Vertex2D, int> rightVertices,
        int indexTextureVL, int indexTextureVR1, int indexTextureVR2,
        IDictionary<Vertex2D, int> leftTextureVertices, IDictionary<Vertex2D, int> rightTextureVertices,
        int materialIndex, ICollection<Face<Vertex2D>> leftFaces, ICollection<Face<Vertex2D>> rightFaces)
    {
        var vL = Vertices[indexVL];
        var vR1 = Vertices[indexVR1];
        var vR2 = Vertices[indexVR2];

        var tVL = TextureVertices[indexTextureVL];
        var tVR1 = TextureVertices[indexTextureVR1];
        var tVR2 = TextureVertices[indexTextureVR2];

        var indexVLLeft = leftVertices.AddIndex(vL);
        var indexTextureVLLeft = leftTextureVertices.AddIndex(tVL);

        if (Math.Abs(vR1.x - q) < double.Epsilon && Math.Abs(vR2.x - q) < double.Epsilon)
        {
            //Console.WriteLine("Right Vertices are on the line");

            var indexVR1Left = leftVertices.AddIndex(vR1);
            var indexVR2Left = leftVertices.AddIndex(vR2);

            var indexTextureVR1Left = leftTextureVertices.AddIndex(tVR1);
            var indexTextureVR2Left = leftTextureVertices.AddIndex(tVR2);

            leftFaces.Add(new Face<Vertex2D>(indexVLLeft, indexVR1Left, indexVR2Left, vL, vR1, vR2,
                indexTextureVLLeft, indexTextureVR1Left, indexTextureVR2Left, materialIndex,
                tVL, tVR1, tVR2));

            return;
        }

        var indexVR1Right = rightVertices.AddIndex(vR1);
        var indexVR2Right = rightVertices.AddIndex(vR2);

        //Console.WriteLine("a on the left, b and c on the right");

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
        var t1t = utils.CutEdgePerc(tVL, tVR1, perc1);
        var indexTextureT1Left = leftTextureVertices.AddIndex(t1t);
        var indexTextureT1Right = rightTextureVertices.AddIndex(t1t);

        var perc2 = GetIntersectionPerc(vL, vR2, t2);

        // Seconda intersezione texture
        var t2t = utils.CutEdgePerc(tVL, tVR2, perc2);
        var indexTextureT2Left = leftTextureVertices.AddIndex(t2t);
        var indexTextureT2Right = rightTextureVertices.AddIndex(t2t);

        var lface = new Face<Vertex2D>(indexVLLeft, indexT1Left, indexT2Left, vL, t1, t2,
            indexTextureVLLeft, indexTextureT1Left, indexTextureT2Left, materialIndex,
            tVL, t1t, t2t);
        leftFaces.Add(lface);

        //Console.WriteLine($"Left face: {lface}");

        var rface1 = new Face<Vertex2D>(indexT1Right, indexVR1Right, indexVR2Right, t1, vR1, vR2,
            indexTextureT1Right, indexTextureVR1Right, indexTextureVR2Right, materialIndex,
            t1t, tVR1, tVR2);
        rightFaces.Add(rface1);

        //Console.WriteLine($"Right face 1: {rface1}");

        var rface2 = new Face<Vertex2D>(indexT1Right, indexVR2Right, indexT2Right, t1, vR2, t2,
            indexTextureT1Right, indexTextureVR2Right, indexTextureT2Right, materialIndex,
            t1t, tVR2, t2t);
        rightFaces.Add(rface2);


        //Console.WriteLine($"Right face 2: {rface2}");
    }



    private void IntersectRight2DWithTexture(IVertexUtils utils, double q, int indexVR, int indexVL1, int indexVL2,
        IDictionary<Vertex2D, int> leftVertices, IDictionary<Vertex2D, int> rightVertices,
        int indexTextureVR, int indexTextureVL1, int indexTextureVL2,
        IDictionary<Vertex2D, int> leftTextureVertices, IDictionary<Vertex2D, int> rightTextureVertices,
        int materialIndex, ICollection<Face<Vertex2D>> leftFaces, ICollection<Face<Vertex2D>> rightFaces)
    {
        var vR = Vertices[indexVR];
        var vL1 = Vertices[indexVL1];
        var vL2 = Vertices[indexVL2];

        var tVR = TextureVertices[indexTextureVR];
        var tVL1 = TextureVertices[indexTextureVL1];
        var tVL2 = TextureVertices[indexTextureVL2];

        var indexVRRight = rightVertices.AddIndex(vR);
        var indexTextureVRRight = rightTextureVertices.AddIndex(tVR);

        if (Math.Abs(utils.GetDimension(vL1) - q) < double.Epsilon && Math.Abs(utils.GetDimension(vL2) - q) < double.Epsilon)
        {
            //Console.WriteLine("Left Vertices are on the line");

            var indexVL1Right = rightVertices.AddIndex(vL1);
            var indexVL2Right = rightVertices.AddIndex(vL2);

            var indexTextureVL1Right = rightTextureVertices.AddIndex(tVL1);
            var indexTextureVL2Right = rightTextureVertices.AddIndex(tVL2);

            rightFaces.Add(new Face<Vertex2D>(indexVRRight, indexVL1Right, indexVL2Right, vR, vL1, vL2,
                indexTextureVRRight, indexTextureVL1Right, indexTextureVL2Right, materialIndex,
                tVR, tVL1, tVL2));

            return;
        }

        var indexVL1Left = leftVertices.AddIndex(vL1);
        var indexVL2Left = leftVertices.AddIndex(vL2);

        //Console.WriteLine("a on the right, b and c on the left");

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
        var t1t = utils.CutEdgePerc(tVR, tVL1, perc1);
        var indexTextureT1Left = leftTextureVertices.AddIndex(t1t);
        var indexTextureT1Right = rightTextureVertices.AddIndex(t1t);

        var perc2 = GetIntersectionPerc(vR, vL2, t2);

        // Seconda intersezione texture
        var t2t = utils.CutEdgePerc(tVR, tVL2, perc2);
        var indexTextureT2Left = leftTextureVertices.AddIndex(t2t);
        var indexTextureT2Right = rightTextureVertices.AddIndex(t2t);

        var rface = new Face<Vertex2D>(indexVRRight, indexT1Right, indexT2Right, vR, t1, t2,
            indexTextureVRRight, indexTextureT1Right, indexTextureT2Right, materialIndex,
            tVR, t1t, t2t);
        rightFaces.Add(rface);

        //Console.WriteLine($"Right face: {rface}");

        var lface1 = new Face<Vertex2D>(indexT2Left, indexVL1Left, indexVL2Left, t2, vL1, vL2,
            indexTextureT2Left, indexTextureVL1Left, indexTextureVL2Left, materialIndex,
            t2t, tVL1, tVL2);
        leftFaces.Add(lface1);

        //Console.WriteLine($"Left face 1: {lface1}");

        var lface2 = new Face<Vertex2D>(indexT2Left, indexT1Left, indexVL1Left, t2, t1, vL1,
            indexTextureT2Left, indexTextureT1Left, indexTextureVL1Left, materialIndex,
            t2t, t1t, tVL1);
        leftFaces.Add(lface2);


        //Console.WriteLine($"Left face 2: {lface2}");
    }

    public void TrimTextures()
    {
        throw new NotImplementedException();
    }

    #region No texture

    public int SplitWithoutTexture(IVertexUtils utils, double q, out Mesh2D left, out Mesh2D right)
    {
        var leftVertices = new Dictionary<Vertex2D, int>(Vertices.Count);
        var rightVertices = new Dictionary<Vertex2D, int>(Vertices.Count);

        var leftFaces = new List<Face<Vertex2D>>(Faces.Count);
        var rightFaces = new List<Face<Vertex2D>>(Faces.Count);

        var count = 0;

        foreach (var face in Faces)
        {
            //Console.WriteLine(face);

            var aSide = face.A.x < q;
            var bSide = face.B.x < q;
            var cSide = face.C.x < q;

            if (aSide)
            {
                if (bSide)
                {
                    if (cSide)
                    {
                        var indexALeft = leftVertices.AddIndex(face.A);
                        var indexBLeft = leftVertices.AddIndex(face.B);
                        var indexCLeft = leftVertices.AddIndex(face.C);

                        leftFaces.Add(new Face<Vertex2D>(indexALeft, indexBLeft, indexCLeft, face.A, face.B, face.C));

                        //Console.WriteLine("All on the left");
                    }
                    else
                    {
                        IntersectRight2D(utils, q, face.IndexC, face.IndexB, face.IndexA, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectRight2D(utils, q, face.IndexB, face.IndexA, face.IndexC, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2D(utils, q, face.IndexA, face.IndexB, face.IndexC, leftVertices, rightVertices,
                            leftFaces, rightFaces);
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
                        IntersectRight2D(utils, q, face.IndexA, face.IndexB, face.IndexC, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2D(utils, q, face.IndexB, face.IndexA, face.IndexC, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectLeft2D(utils, q, face.IndexC, face.IndexA, face.IndexB, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                    else
                    {
                        var indexARight = rightVertices.AddIndex(face.A);
                        var indexBRight = rightVertices.AddIndex(face.B);
                        var indexCRight = rightVertices.AddIndex(face.C);
                        rightFaces.Add(
                            new Face<Vertex2D>(indexARight, indexBRight, indexCRight, face.A, face.B, face.C));

                        //Console.WriteLine("All on the right");
                    }
                }
            }
        }

        var orderedLeftVertices = leftVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();
        var orderedRightVertices = rightVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();

        // Aggiungere split texture
        left = new Mesh2D(orderedLeftVertices, TextureVertices, leftFaces, Materials);
        right = new Mesh2D(orderedRightVertices, TextureVertices, rightFaces, Materials);

        return count;
    }

    private void IntersectLeft2D(IVertexUtils utils, double q, int indexVL, int indexVR1, int indexVR2,
        IDictionary<Vertex2D, int> leftVertices,
        IDictionary<Vertex2D, int> rightVertices, ICollection<Face<Vertex2D>> leftFaces,
        ICollection<Face<Vertex2D>> rightFaces)
    {
        var vL = Vertices[indexVL];
        var vR1 = Vertices[indexVR1];
        var vR2 = Vertices[indexVR2];

        var indexVLLeft = leftVertices.AddIndex(vL);

        if (Math.Abs(utils.GetDimension(vR1) - q) < double.Epsilon && Math.Abs(utils.GetDimension(vR2) - q) < double.Epsilon)
        {
            //Console.WriteLine("Right Vertices are on the line");

            var indexVR1Left = leftVertices.AddIndex(vR1);
            var indexVR2Left = leftVertices.AddIndex(vR2);

            leftFaces.Add(new Face<Vertex2D>(indexVLLeft, indexVR1Left, indexVR2Left, vL, vR1, vR2));
            return;
        }

        var indexVR1Right = rightVertices.AddIndex(vR1);
        var indexVR2Right = rightVertices.AddIndex(vR2);

        //Console.WriteLine("a on the left, b and c on the right");

        // Prima intersezione
        var t1 = utils.CutEdge(vL, vR1, q);
        var indexT1Left = leftVertices.AddIndex(t1);
        var indexT1Right = rightVertices.AddIndex(t1);

        // Seconda intersezione
        var t2 = utils.CutEdge(vL, vR2, q);
        var indexT2Left = leftVertices.AddIndex(t2);
        var indexT2Right = rightVertices.AddIndex(t2);

        var lface = new Face<Vertex2D>(indexVLLeft, indexT1Left, indexT2Left, vL, t1, t2);
        leftFaces.Add(lface);

        //Console.WriteLine($"Left face: {lface}");

        var rface1 = new Face<Vertex2D>(indexT1Right, indexVR1Right, indexVR2Right, t1, vR1, vR2);
        rightFaces.Add(rface1);

        //Console.WriteLine($"Right face 1: {rface1}");

        var rface2 = new Face<Vertex2D>(indexT2Right, indexT1Right, indexVR2Right, t2, t1, vR2);
        rightFaces.Add(rface2);

        //Console.WriteLine($"Right face 2: {rface2}");
    }

    private void IntersectRight2D(IVertexUtils utils, double q, int indexVR, int indexVL1, int indexVL2,
        IDictionary<Vertex2D, int> leftVertices, IDictionary<Vertex2D, int> rightVertices,
        ICollection<Face<Vertex2D>> leftFaces, ICollection<Face<Vertex2D>> rightFaces)
    {
        var vR = Vertices[indexVR];
        var vL1 = Vertices[indexVL1];
        var vL2 = Vertices[indexVL2];

        var indexVRRight = rightVertices.AddIndex(vR);

        if (Math.Abs(utils.GetDimension(vL1) - q) < double.Epsilon && Math.Abs(utils.GetDimension(vL2) - q) < double.Epsilon)
        {
            //Console.WriteLine("Left Vertices are on the line");

            var indexVL1Right = rightVertices.AddIndex(vL1);
            var indexVL2Right = rightVertices.AddIndex(vL2);

            rightFaces.Add(new Face<Vertex2D>(indexVRRight, indexVL1Right, indexVL2Right, vR, vL1, vL2));

            return;
        }

        var indexVL1Left = leftVertices.AddIndex(vL1);
        var indexVL2Left = leftVertices.AddIndex(vL2);

        //Console.WriteLine("a on the right, b and c on the left");

        // Prima intersezione
        var t1 = utils.CutEdge(vR, vL1, q);
        var indexT1Left = leftVertices.AddIndex(t1);
        var indexT1Right = rightVertices.AddIndex(t1);

        // Seconda intersezione
        var t2 = utils.CutEdge(vR, vL2, q);
        var indexT2Left = leftVertices.AddIndex(t2);
        var indexT2Right = rightVertices.AddIndex(t2);

        var rface = new Face<Vertex2D>(indexVRRight, indexT2Right, indexT1Right, vR, t2, t1);
        rightFaces.Add(rface);

        //Console.WriteLine($"Right face: {rface}");

        var lface1 = new Face<Vertex2D>(indexT2Left, indexVL2Left, indexVL1Left, t2, vL2, vL1);
        leftFaces.Add(lface1);

        //Console.WriteLine($"Left face 1: {lface1}");

        var lface2 = new Face<Vertex2D>(indexT2Left, indexVL1Left, indexT1Left, t2, vL1, t1);
        leftFaces.Add(lface2);

        //Console.WriteLine($"Left face 2: {lface2}");
    }

    #endregion

    #region Utils

    /// <summary>
    /// Gets the distance of P from A (in percent) relative to segment AB
    /// </summary>
    /// <param name="a">Edge start</param>
    /// <param name="b">Edge end</param>
    /// <param name="p">Point on the segment</param>
    /// <returns></returns>
    private static double GetIntersectionPerc(Vertex2D a, Vertex2D b, Vertex2D p)
    {
        var edge1Length = a.Distance(b);
        var subEdge1Length = a.Distance(p);
        var perc1 = subEdge1Length / edge1Length;
        return perc1;
    }

    private static readonly CultureInfo en = CultureInfo.GetCultureInfo("en-US");

    public void WriteObj(string path)
    {
        var materialsPath = Path.ChangeExtension(path, "mtl");

        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("mtllib {0}", Path.GetFileName(materialsPath));
            writer.Write("o ");
            writer.WriteLine(string.IsNullOrWhiteSpace(Name) ? DefaultName : Name);

            CultureInfo.CurrentCulture = en;

            foreach (var vertex in Vertices)
            {
                writer.Write("v ");
                writer.Write(vertex.x);
                writer.Write(" ");
                writer.Write(vertex.y);
                writer.WriteLine(" 0");
                //writer.Write(vertex.z);
                //writer.WriteLine(string.Format(en, "v {0} {1} 0", vertex.x, vertex.y));
            }

            foreach (var textureVertex in TextureVertices)
            {
                writer.Write("vt ");
                writer.Write(textureVertex.x);
                writer.Write(" ");
                writer.WriteLine(textureVertex.y);

                //writer.WriteLine(string.Format(en, "vt {0} {1}", textureVertex.x, textureVertex.y));
            }

            var materialFaces = from face in Faces
                group face by face.MaterialIndex
                into g
                select g;

            // NOTE: Se ci sono gruppi di facce senza materiali vanno messe all'inizio
            foreach (var grp in materialFaces.OrderBy(item => item.Key))
            {
                if (grp.Key != null)
                    writer.WriteLine($"usemtl {Materials[grp.Key.Value].Name}");

                foreach (var face in grp)
                    writer.WriteLine(face.ToObj());
            }
        }

        
        using (var writer = new StreamWriter(Path.ChangeExtension(path, "mtl")))
        {
            foreach (var material in Materials)
            {
                writer.WriteLine(material.ToMtl());
            }
        }
    }

    #endregion
}