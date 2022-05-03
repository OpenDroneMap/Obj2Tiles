namespace Obj2Tiles.Library.Geometry;

internal class Edge : IEquatable<Edge>
{
    public readonly int V1Index;
    public readonly int V2Index;

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

    }

    public override bool Equals(object? obj)
    {
        return !ReferenceEquals(null, obj) &&
               (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Edge)obj));
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(V1Index, V2Index);
    }

    public bool Equals(Edge? other)
    {
        return !ReferenceEquals(null, other) &&
               (ReferenceEquals(this, other) || V1Index == other.V1Index && V2Index == other.V2Index);
    }
}