using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Shouldly;

namespace Obj2Tiles.Library.Test;

/// <summary>
/// Tests for the VertexMedian split strategy — balanced splitting based on vertex median.
/// These tests validate GetVertexMedian(), RecurseSplitXYBalanced, and RecurseSplitXYZBalanced.
///
/// NOTE: These tests will compile only after implementing GetVertexMedian() on IMesh/Mesh/MeshT
/// and RecurseSplitXYBalanced/RecurseSplitXYZBalanced on MeshUtils.
/// </summary>
public class VertexMedianSplitTests
{
    private const string TestDataPath = "TestData";

    private static readonly IVertexUtils XUtils = new VertexUtilsX();
    private static readonly IVertexUtils YUtils = new VertexUtilsY();
    private static readonly IVertexUtils ZUtils = new VertexUtilsZ();

    private IMesh LoadCube3D() => MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-3d.obj"));
    private IMesh LoadCubeColors3D() => MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube-colors-3d.obj"));
    private IMesh LoadCubeTextured() => MeshUtils.LoadMesh(Path.Combine(TestDataPath, "cube2", "cube.obj"));

    private static Vertex3 GetMedian(IMesh mesh) => mesh.GetVertexMedian();

    // ===================================================================
    // Helper: mesh sintetico con distribuzione non uniforme
    // ===================================================================

    /// <summary>
    /// Crea un mesh con vertici distribuiti non uniformemente:
    /// - 80 vertici nel cluster denso X∈[0,1], Y∈[0,1]
    /// - 20 vertici nel cluster sparso X∈[9,10], Y∈[9,10]
    /// Tutti con Z=0, collegati come triangoli fan dal vertice 0.
    /// </summary>
    private static Mesh CreateNonUniformMesh()
    {
        var rng = new Random(42); // seed deterministico per riproducibilità
        var vertices = new List<Vertex3>();
        var faces = new List<Face>();

        // Cluster denso (80% dei vertici)
        for (var i = 0; i < 80; i++)
            vertices.Add(new Vertex3(rng.NextDouble(), rng.NextDouble(), 0));

        // Cluster sparso (20% dei vertici)
        for (var i = 0; i < 20; i++)
            vertices.Add(new Vertex3(9 + rng.NextDouble(), 9 + rng.NextDouble(), 0));

        // Fan triangulation dal vertice 0
        for (var i = 1; i < vertices.Count - 1; i++)
            faces.Add(new Face(0, i, i + 1));

        return new Mesh(vertices, faces);
    }

    /// <summary>
    /// Crea un mesh con vertici distribuiti uniformemente in un cubo [0,10]³.
    /// </summary>
    private static Mesh CreateUniformMesh()
    {
        var rng = new Random(42);
        var vertices = new List<Vertex3>();
        var faces = new List<Face>();

        for (var i = 0; i < 100; i++)
            vertices.Add(new Vertex3(rng.NextDouble() * 10, rng.NextDouble() * 10, rng.NextDouble() * 10));

        for (var i = 1; i < vertices.Count - 1; i++)
            faces.Add(new Face(0, i, i + 1));

        return new Mesh(vertices, faces);
    }

    // ===================================================================
    // Gruppo 1: GetVertexMedian — correttezza
    // ===================================================================

    [Test]
    public void GetVertexMedian_Triangle_ReturnsMedianCoordinates()
    {
        // 3 vertici: (0,0,0), (10,4,2), (3,8,6)
        // Mediana X: sorted [0,3,10] → mid=1 → 3
        // Mediana Y: sorted [0,4,8] → mid=1 → 4
        // Mediana Z: sorted [0,2,6] → mid=1 → 2
        var vertices = new List<Vertex3>
        {
            new(0, 0, 0),
            new(10, 4, 2),
            new(3, 8, 6)
        };
        var faces = new List<Face> { new(0, 1, 2) };
        var mesh = new Mesh(vertices, faces);

        var median = mesh.GetVertexMedian();

        median.X.ShouldBe(3);
        median.Y.ShouldBe(4);
        median.Z.ShouldBe(2);
    }

    [Test]
    public void GetVertexMedian_EvenCount_ReturnsUpperMedian()
    {
        // 4 vertici in X: [1, 3, 7, 9] → mid=4/2=2 → xs[2]=7
        var vertices = new List<Vertex3>
        {
            new(1, 10, 100),
            new(3, 30, 300),
            new(7, 70, 700),
            new(9, 90, 900)
        };
        var faces = new List<Face> { new(0, 1, 2), new(0, 2, 3) };
        var mesh = new Mesh(vertices, faces);

        var median = mesh.GetVertexMedian();

        median.X.ShouldBe(7);
        median.Y.ShouldBe(70);
        median.Z.ShouldBe(700);
    }

    [Test]
    public void GetVertexMedian_SingleVertex_ReturnsThatVertex()
    {
        var vertices = new List<Vertex3> { new(5, 3, 7) };
        // Serve almeno un triangolo per avere un mesh valido
        // ma possiamo avere una faccia degenere — oppure testare senza facce
        var faces = new List<Face>();
        var mesh = new Mesh(vertices, faces);

        var median = mesh.GetVertexMedian();

        median.X.ShouldBe(5);
        median.Y.ShouldBe(3);
        median.Z.ShouldBe(7);
    }

    [Test]
    public void GetVertexMedian_CubeFile_WithinBounds()
    {
        var mesh = LoadCube3D();
        var bounds = mesh.Bounds;

        var median = mesh.GetVertexMedian();

        median.X.ShouldBeInRange(bounds.Min.X, bounds.Max.X);
        median.Y.ShouldBeInRange(bounds.Min.Y, bounds.Max.Y);
        median.Z.ShouldBeInRange(bounds.Min.Z, bounds.Max.Z);
    }

    [Test]
    public void GetVertexMedian_NonUniformMesh_DiffersFromBaricenter()
    {
        var mesh = CreateNonUniformMesh();

        var median = mesh.GetVertexMedian();
        var baricenter = mesh.GetVertexBaricenter();

        // Con 80% dei vertici in [0,1] e 20% in [9,10]:
        // Il baricentro X è spostato verso il cluster denso ma influenzato dal cluster sparso: ~2.x
        // La mediana X è nel cluster denso: ~0.x (il 50-esimo vertice ordinato per X è nel primo cluster)
        // Quindi la mediana è significativamente più bassa del baricentro in X
        median.X.ShouldBeLessThan(baricenter.X);
    }

    // ===================================================================
    // Gruppo 2: Split bilanciato — proprietà strutturali
    // ===================================================================

    [Test]
    public void SplitX_AtMedian_BalancesVertexCount()
    {
        var mesh = CreateNonUniformMesh();
        var median = mesh.GetVertexMedian();

        mesh.Split(XUtils, median.X, out var left, out var right);

        // La mediana dovrebbe dividere i vertici approssimativamente a metà.
        // Con i triangoli tagliati ci saranno vertici aggiuntivi, ma il rapporto
        // dovrebbe essere ragionevole (entro un fattore 2).
        var ratio = (double)left.VertexCount / right.VertexCount;
        ratio.ShouldBeInRange(0.3, 3.0);
    }

    [Test]
    public void SplitX_AtMedian_MoreBalancedThanBaricenter()
    {
        var mesh = CreateNonUniformMesh();

        var median = mesh.GetVertexMedian();
        var baricenter = mesh.GetVertexBaricenter();

        // Split alla mediana
        mesh.Split(XUtils, median.X, out var leftMedian, out var rightMedian);
        var ratioMedian = Math.Abs(leftMedian.VertexCount - rightMedian.VertexCount);

        // Split al baricentro — serve un nuovo mesh perché Split è consumante
        var mesh2 = CreateNonUniformMesh();
        mesh2.Split(XUtils, baricenter.X, out var leftBari, out var rightBari);
        var ratioBari = Math.Abs(leftBari.VertexCount - rightBari.VertexCount);

        // La mediana dovrebbe essere più bilanciata (differenza minore)
        ratioMedian.ShouldBeLessThan(ratioBari);
    }

    [Test]
    public async Task RecurseSplitXYBalanced_Depth1_ProducesUpTo4Meshes()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYBalanced(mesh, 1, GetMedian, bag);

        bag.Count.ShouldBeGreaterThan(0);
        bag.Count.ShouldBeLessThanOrEqualTo(4);

        foreach (var m in bag)
            m.FacesCount.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task RecurseSplitXYZBalanced_Depth1_ProducesUpTo8Meshes()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYZBalanced(mesh, 1, GetMedian, bag);

        bag.Count.ShouldBeGreaterThan(0);
        bag.Count.ShouldBeLessThanOrEqualTo(8);

        foreach (var m in bag)
            m.FacesCount.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task RecurseSplitXYBalanced_TotalFacesAtLeastOriginal()
    {
        var mesh = LoadCube3D();
        var originalFaces = mesh.FacesCount;
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYBalanced(mesh, 1, GetMedian, bag);

        var totalFaces = bag.Sum(m => m.FacesCount);
        totalFaces.ShouldBeGreaterThanOrEqualTo(originalFaces);
    }

    [Test]
    public async Task RecurseSplitXYBalanced_AllMeshesHaveFaces()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYBalanced(mesh, 2, GetMedian, bag);

        foreach (var m in bag)
            m.FacesCount.ShouldBeGreaterThan(0, $"Mesh '{m.Name}' has no faces");
    }

    [Test]
    public async Task RecurseSplitXYBalanced_UnionBoundsCoversOriginal()
    {
        var mesh = LoadCube3D();
        var originalBounds = mesh.Bounds;
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYBalanced(mesh, 1, GetMedian, bag);

        var allMeshes = bag.ToList();
        var minX = allMeshes.Min(m => m.Bounds.Min.X);
        var minY = allMeshes.Min(m => m.Bounds.Min.Y);
        var maxX = allMeshes.Max(m => m.Bounds.Max.X);
        var maxY = allMeshes.Max(m => m.Bounds.Max.Y);

        minX.ShouldBeLessThanOrEqualTo(originalBounds.Min.X + 1e-9);
        minY.ShouldBeLessThanOrEqualTo(originalBounds.Min.Y + 1e-9);
        maxX.ShouldBeGreaterThanOrEqualTo(originalBounds.Max.X - 1e-9);
        maxY.ShouldBeGreaterThanOrEqualTo(originalBounds.Max.Y - 1e-9);
    }

    // ===================================================================
    // Gruppo 3: Bilanciamento effettivo su mesh non uniforme
    // ===================================================================

    [Test]
    public async Task RecurseSplitXYBalanced_NonUniform_TilesHaveSimilarVertexCount()
    {
        var mesh = CreateNonUniformMesh();
        var bagMedian = new ConcurrentBag<IMesh>();
        var bagCenter = new ConcurrentBag<IMesh>();

        // Split bilanciato con mediana
        await MeshUtils.RecurseSplitXYBalanced(mesh, 1, GetMedian, bagMedian);

        // Split non bilanciato con centro bounding box (per confronto)
        var mesh2 = CreateNonUniformMesh();
        await MeshUtils.RecurseSplitXY(mesh2, 1, m => m.Bounds.Center, bagCenter);

        // La deviazione standard dei conteggi vertici dev'essere minore con la mediana
        var medianCounts = bagMedian.Select(m => (double)m.VertexCount).ToArray();
        var centerCounts = bagCenter.Select(m => (double)m.VertexCount).ToArray();

        var stdMedian = StdDev(medianCounts);
        var stdCenter = StdDev(centerCounts);

        stdMedian.ShouldBeLessThan(stdCenter);
    }

    [Test]
    public async Task RecurseSplitXYBalanced_Depth2_MaintainsBalance()
    {
        var mesh = CreateNonUniformMesh();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYBalanced(mesh, 2, GetMedian, bag);

        var counts = bag.Select(m => m.VertexCount).ToArray();
        counts.Length.ShouldBeGreaterThan(0);

        // Il rapporto tra tile più grande e più piccola dovrebbe essere ragionevole
        var max = counts.Max();
        var min = counts.Min();
        var ratio = (double)max / min;

        // Con mediana, il rapporto max/min dovrebbe essere < 10x anche a depth=2
        // (con solo 100 vertici e topologia fan, il edge cutting crea sbilanciamenti)
        ratio.ShouldBeLessThan(10.0);
    }

    [Test]
    public async Task RecurseSplitXYBalanced_Uniform_AllTilesRoughlyEqual()
    {
        var mesh = CreateUniformMesh();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYBalanced(mesh, 1, GetMedian, bag);

        var counts = bag.Select(m => m.VertexCount).ToArray();
        counts.Length.ShouldBeGreaterThan(0);

        // Per una distribuzione uniforme, la mediana ~= il centro,
        // quindi tutti i quadranti dovrebbero avere ~25% dei vertici
        var avg = counts.Average();
        foreach (var count in counts)
        {
            // Entro ±50% dalla media (tolleranza per i vertici generati dai tagli)
            count.ShouldBeGreaterThan((int)(avg * 0.5));
            count.ShouldBeLessThan((int)(avg * 1.5) + 1);
        }
    }

    // ===================================================================
    // Gruppo 4: Preservazione dati attraverso split bilanciato
    // ===================================================================

    [Test]
    public async Task RecurseSplitXYBalanced_WithColors_ColorsPreserved()
    {
        var mesh = LoadCubeColors3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYBalanced(mesh, 1, GetMedian, bag);

        bag.Count.ShouldBeGreaterThan(0);

        foreach (var m in bag)
        {
            var typedMesh = m.ShouldBeOfType<Mesh>();
            typedMesh.VertexColors.ShouldNotBeNull();
            typedMesh.VertexColors!.Count.ShouldBe(m.VertexCount);
        }
    }

    [Test]
    public async Task RecurseSplitXYZBalanced_WithColors_ColorsPreserved()
    {
        var mesh = LoadCubeColors3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYZBalanced(mesh, 1, GetMedian, bag);

        bag.Count.ShouldBeGreaterThan(0);

        foreach (var m in bag)
        {
            var typedMesh = m.ShouldBeOfType<Mesh>();
            typedMesh.VertexColors.ShouldNotBeNull();
            typedMesh.VertexColors!.Count.ShouldBe(m.VertexCount);
        }
    }

    [Test]
    public async Task RecurseSplitXYBalanced_MeshT_PreservesTextures()
    {
        var mesh = LoadCubeTextured();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYBalanced(mesh, 1, GetMedian, bag);

        bag.Count.ShouldBeGreaterThan(0);

        foreach (var m in bag)
            m.ShouldBeOfType<MeshT>();
    }

    // ===================================================================
    // Gruppo 5: Regressione — i metodi bilanciati devono rispettare gli stessi
    // invarianti dei metodi non bilanciati
    // ===================================================================

    [Test]
    public async Task RecurseSplitXYBalanced_NamesAreStructural()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYBalanced(mesh, 1, GetMedian, bag);

        // Ogni nome deve contenere L o R per ciascun asse splittato
        foreach (var m in bag)
        {
            m.Name.ShouldContain("X", Case.Insensitive);
            m.Name.ShouldContain("Y", Case.Insensitive);
        }
    }

    [Test]
    public async Task RecurseSplitXYZBalanced_NamesContainAllAxes()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYZBalanced(mesh, 1, GetMedian, bag);

        foreach (var m in bag)
        {
            m.Name.ShouldContain("X", Case.Insensitive);
            m.Name.ShouldContain("Y", Case.Insensitive);
            m.Name.ShouldContain("Z", Case.Insensitive);
        }
    }

    [Test]
    public async Task RecurseSplitXYBalanced_Depth2_ProducesUpTo16Meshes()
    {
        var mesh = LoadCube3D();
        var bag = new ConcurrentBag<IMesh>();

        await MeshUtils.RecurseSplitXYBalanced(mesh, 2, GetMedian, bag);

        bag.Count.ShouldBeGreaterThan(0);
        bag.Count.ShouldBeLessThanOrEqualTo(16);
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    private static double StdDev(double[] values)
    {
        if (values.Length == 0) return 0;
        var avg = values.Average();
        var sumSqDiff = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSqDiff / values.Length);
    }
}
