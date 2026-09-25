namespace Obj2Tiles.Library.Geometry;

public interface IMesh
{
    string Name { get; set; }
    string DebugName { get; set; }
    Box3 Bounds { get; }
    double AverageEdgeLength { get; }
    double MaximumEdgeLength { get; }
    IReadOnlyList<Vertex3> Vertices { get; }

    int Split(IVertexUtils utils, double q, out IMesh left,
        out IMesh right);

    Vertex3 GetVertexBaricenter();
    Vertex3 GetVertexMedian();
    void WriteObj(string path, bool removeUnused = true);
    void Translate(Vertex3 offset);

    int FacesCount { get; }
    int VertexCount { get; }
}