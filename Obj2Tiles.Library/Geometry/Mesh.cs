using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Obj2Tiles.Library.Materials;
using SixLabors.ImageSharp;

namespace Obj2Tiles.Library.Geometry;

public class Mesh : IMesh
{
    private List<Vertex3> _vertices;
    private readonly List<Face> _faces;
    private List<RGB>? _vertexColors;

    public IReadOnlyList<Vertex3> Vertices => _vertices;
    public IReadOnlyList<Face> Faces => _faces;
    public IReadOnlyList<RGB>? VertexColors => _vertexColors;

    public const string DefaultName = "Mesh";

    public string Name { get; set; } = DefaultName;

    public Mesh(IEnumerable<Vertex3> vertices, IEnumerable<Face> faces, IEnumerable<RGB>? vertexColors = null)
    {
        _vertices = new List<Vertex3>(vertices);
        _faces = new List<Face>(faces);
        _vertexColors = vertexColors != null ? new List<RGB>(vertexColors) : null;
    }

    public int Split(IVertexUtils utils, double q, out IMesh left,
        out IMesh right)
    {
        var leftVertices = new Dictionary<Vertex3, int>(_vertices.Count);
        var rightVertices = new Dictionary<Vertex3, int>(_vertices.Count);

        // Vertex colors share the same indices as vertices.
        // We track them in parallel lists, appending whenever a new vertex is added.
        var hasColors = _vertexColors != null;
        var leftColors = hasColors ? new List<RGB>(_vertices.Count) : null;
        var rightColors = hasColors ? new List<RGB>(_vertices.Count) : null;

        var leftFaces = new List<Face>(_faces.Count);
        var rightFaces = new List<Face>(_faces.Count);

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

                        AddVertexWithColor(leftVertices, leftColors, vA, face.IndexA);
                        AddVertexWithColor(leftVertices, leftColors, vB, face.IndexB);
                        AddVertexWithColor(leftVertices, leftColors, vC, face.IndexC);

                        var indexALeft = leftVertices[vA];
                        var indexBLeft = leftVertices[vB];
                        var indexCLeft = leftVertices[vC];

                        leftFaces.Add(new Face(indexALeft, indexBLeft, indexCLeft));
                    }
                    else
                    {
                        IntersectRight2D(utils, q, face.IndexC, face.IndexA, face.IndexB, leftVertices, rightVertices,
                            leftColors, rightColors, leftFaces, rightFaces);
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectRight2D(utils, q, face.IndexB, face.IndexC, face.IndexA, leftVertices, rightVertices,
                            leftColors, rightColors, leftFaces, rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2D(utils, q, face.IndexA, face.IndexB, face.IndexC, leftVertices, rightVertices,
                            leftColors, rightColors, leftFaces, rightFaces);
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
                            leftColors, rightColors, leftFaces, rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2D(utils, q, face.IndexB, face.IndexC, face.IndexA, leftVertices, rightVertices,
                            leftColors, rightColors, leftFaces, rightFaces);
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectLeft2D(utils, q, face.IndexC, face.IndexA, face.IndexB, leftVertices, rightVertices,
                            leftColors, rightColors, leftFaces, rightFaces);
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
                        rightFaces.Add(new Face(indexARight, indexBRight, indexCRight));
                    }
                }
            }
        }

        var orderedLeftVertices = leftVertices.OrderBy(x => x.Value).Select(x => x.Key);
        var orderedRightVertices = rightVertices.OrderBy(x => x.Value).Select(x => x.Key);

        IEnumerable<RGB>? orderedLeftColors = null;
        IEnumerable<RGB>? orderedRightColors = null;
        if (hasColors)
        {
            // Colors are already in order (appended as vertices were added)
            orderedLeftColors = leftColors;
            orderedRightColors = rightColors;
        }

        left = new Mesh(orderedLeftVertices, leftFaces, orderedLeftColors)
        {
            Name = $"{Name}-{utils.Axis}L"
        };

        right = new Mesh(orderedRightVertices, rightFaces, orderedRightColors)
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

    private void IntersectLeft2D(IVertexUtils utils, double q, int indexVL, int indexVR1, int indexVR2,
        IDictionary<Vertex3, int> leftVertices,
        IDictionary<Vertex3, int> rightVertices,
        List<RGB>? leftColors, List<RGB>? rightColors,
        ICollection<Face> leftFaces,
        ICollection<Face> rightFaces)
    {
        var vL = _vertices[indexVL];
        var vR1 = _vertices[indexVR1];
        var vR2 = _vertices[indexVR2];

        AddVertexWithColor(leftVertices, leftColors, vL, indexVL);
        var indexVLLeft = leftVertices[vL];

        if (Math.Abs(utils.GetDimension(vR1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vR2) - q) < Common.Epsilon)
        {
            // Right Vertices are on the line

            AddVertexWithColor(leftVertices, leftColors, vR1, indexVR1);
            AddVertexWithColor(leftVertices, leftColors, vR2, indexVR2);
            var indexVR1Left = leftVertices[vR1];
            var indexVR2Left = leftVertices[vR2];

            leftFaces.Add(new Face(indexVLLeft, indexVR1Left, indexVR2Left));
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
            var perc1 = Common.GetIntersectionPerc(vL, vR1, t1);
            t1Color = _vertexColors[indexVL].CutEdgePerc(_vertexColors[indexVR1], perc1);
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
            var perc2 = Common.GetIntersectionPerc(vL, vR2, t2);
            t2Color = _vertexColors[indexVL].CutEdgePerc(_vertexColors[indexVR2], perc2);
        }
        AddIntersectionVertexWithColor(leftVertices, leftColors, t2, t2Color);
        AddIntersectionVertexWithColor(rightVertices, rightColors, t2, t2Color);
        var indexT2Left = leftVertices[t2];
        var indexT2Right = rightVertices[t2];

        var lface = new Face(indexVLLeft, indexT1Left, indexT2Left);
        leftFaces.Add(lface);

        var rface1 = new Face(indexT1Right, indexVR1Right, indexVR2Right);
        rightFaces.Add(rface1);

        var rface2 = new Face(indexT1Right, indexVR2Right, indexT2Right);
        rightFaces.Add(rface2);
    }

    private void IntersectRight2D(IVertexUtils utils, double q, int indexVR, int indexVL1, int indexVL2,
        IDictionary<Vertex3, int> leftVertices, IDictionary<Vertex3, int> rightVertices,
        List<RGB>? leftColors, List<RGB>? rightColors,
        ICollection<Face> leftFaces, ICollection<Face> rightFaces)
    {
        var vR = _vertices[indexVR];
        var vL1 = _vertices[indexVL1];
        var vL2 = _vertices[indexVL2];

        AddVertexWithColor(rightVertices, rightColors, vR, indexVR);
        var indexVRRight = rightVertices[vR];

        if (Math.Abs(utils.GetDimension(vL1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vL2) - q) < Common.Epsilon)
        {
            // Left Vertices are on the line
            AddVertexWithColor(rightVertices, rightColors, vL1, indexVL1);
            AddVertexWithColor(rightVertices, rightColors, vL2, indexVL2);
            var indexVL1Right = rightVertices[vL1];
            var indexVL2Right = rightVertices[vL2];

            rightFaces.Add(new Face(indexVRRight, indexVL1Right, indexVL2Right));

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
            var perc1 = Common.GetIntersectionPerc(vR, vL1, t1);
            t1Color = _vertexColors[indexVR].CutEdgePerc(_vertexColors[indexVL1], perc1);
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
            var perc2 = Common.GetIntersectionPerc(vR, vL2, t2);
            t2Color = _vertexColors[indexVR].CutEdgePerc(_vertexColors[indexVL2], perc2);
        }
        AddIntersectionVertexWithColor(leftVertices, leftColors, t2, t2Color);
        AddIntersectionVertexWithColor(rightVertices, rightColors, t2, t2Color);
        var indexT2Left = leftVertices[t2];
        var indexT2Right = rightVertices[t2];

        var rface = new Face(indexVRRight, indexT1Right, indexT2Right);
        rightFaces.Add(rface);

        var lface1 = new Face(indexT2Left, indexVL1Left, indexVL2Left);
        leftFaces.Add(lface1);

        var lface2 = new Face(indexT2Left, indexT1Left, indexVL1Left);
        leftFaces.Add(lface2);
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

                maxX = v.X < maxX ? maxX : v.X;
                maxY = v.Y < maxY ? maxY : v.Y;
                maxZ = v.Z < maxZ ? maxZ : v.Z;
            }

            return new Box3(minX, minY, minZ, maxX, maxY, maxZ);
        }
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

        if (removeUnused) RemoveUnusedVertices();

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

    public int FacesCount => _faces.Count;
    public int VertexCount => _vertices.Count;

    #endregion


}
