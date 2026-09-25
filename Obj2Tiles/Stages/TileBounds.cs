using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles.Stages;

public readonly record struct TileBounds(Box3 Box, double AverageEdgeLength, double MaximumEdgeLength, int FacesCount);
