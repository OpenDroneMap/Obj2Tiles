using System.Collections.Generic;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Library.Materials;
using Shouldly;

namespace Obj2Tiles.Library.Test;

public class UvIslandTests
{
    private static List<List<int>> Islands(MeshT mesh, params int[] faces) =>
        MeshT.GetFacesClusters(faces,
            MeshT.GetFacesMapper(mesh.GetEdgesMapper(faces, mesh.GetCanonicalPositionIndices())));

    [Test]
    public void FacesSharingUvIndicesButNoGeometry_AreSeparateIslands()
    {
        var mesh = new MeshT(
            [
                new Vertex3(0, 0, 0), new Vertex3(1, 0, 0), new Vertex3(0, 1, 0),
                new Vertex3(10, 0, 0), new Vertex3(11, 0, 0), new Vertex3(10, 1, 0)
            ],
            [new Vertex2(0, 0), new Vertex2(1, 0), new Vertex2(0, 1)],
            [new FaceT(0, 1, 2, 0, 1, 2, 0), new FaceT(3, 4, 5, 0, 1, 2, 0)],
            [new Material("m")]);

        Islands(mesh, 0, 1).Count.ShouldBe(2);
    }

    [Test]
    public void FacesSharingAnEdgeThroughDuplicatedPositions_AreOneIsland()
    {
        // Exporter-style duplicate vertices: indices 3/4 repeat the positions of 1/2.
        var mesh = new MeshT(
            [
                new Vertex3(0, 0, 0), new Vertex3(1, 0, 0), new Vertex3(0, 1, 0),
                new Vertex3(1, 0, 0), new Vertex3(0, 1, 0), new Vertex3(1, 1, 0)
            ],
            [new Vertex2(0, 0), new Vertex2(1, 0), new Vertex2(0, 1), new Vertex2(1, 1)],
            [new FaceT(0, 1, 2, 0, 1, 2, 0), new FaceT(3, 5, 4, 1, 3, 2, 0)],
            [new Material("m")]);

        Islands(mesh, 0, 1).Count.ShouldBe(1);
    }

    [Test]
    public void FacesSharingGeometryAcrossAUvSeam_AreSeparateIslands()
    {
        var mesh = new MeshT(
            [new Vertex3(0, 0, 0), new Vertex3(1, 0, 0), new Vertex3(0, 1, 0), new Vertex3(1, 1, 0)],
            [
                new Vertex2(0, 0), new Vertex2(0.4, 0), new Vertex2(0, 0.4),
                new Vertex2(0.6, 0.6), new Vertex2(1, 0.6), new Vertex2(0.6, 1)
            ],
            [new FaceT(0, 1, 2, 0, 1, 2, 0), new FaceT(1, 3, 2, 3, 4, 5, 0)],
            [new Material("m")]);

        Islands(mesh, 0, 1).Count.ShouldBe(2);
    }
}
