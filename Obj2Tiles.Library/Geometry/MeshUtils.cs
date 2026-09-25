using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Obj2Tiles.Library.Materials;

namespace Obj2Tiles.Library.Geometry;

public class MeshUtils
{
    public static IMesh LoadMesh(string fileName)
    {
        return LoadMesh(fileName, out _);
    }

    public static IMesh LoadMesh(string fileName, out string[] dependencies, bool ignoreNormalMaps = false)
    {
        using var reader = new StreamReader(fileName);

        var vertices = new List<Vertex3>();
        var vertexColors = new List<RGB>();
        var textureVertices = new List<Vertex2>();
        var facesT = new List<FaceT>();
        var faces = new List<Face>();
        var materials = new List<Material>();
        var materialsDict = new Dictionary<string, int>();
        var currentMaterial = string.Empty;
        var deps = new List<string>();

        while (true)
        {
            var line = reader.ReadLine();

            if (line == null) break;

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var segs = line.TrimStart().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            switch (segs[0])
            {
                case "v" when segs.Length >= 4:
                    vertices.Add(new Vertex3(
                        double.Parse(segs[1], CultureInfo.InvariantCulture),
                        double.Parse(segs[2], CultureInfo.InvariantCulture),
                        double.Parse(segs[3], CultureInfo.InvariantCulture)));

                    if (segs.Length >= 7)
                    {
                        vertexColors.Add(new RGB(
                            double.Parse(segs[4], CultureInfo.InvariantCulture),
                            double.Parse(segs[5], CultureInfo.InvariantCulture),
                            double.Parse(segs[6], CultureInfo.InvariantCulture)));
                    }
                    break;
                case "vt" when segs.Length >= 3:

                    var vtx = new Vertex2(
                        double.Parse(segs[1], CultureInfo.InvariantCulture),
                        double.Parse(segs[2], CultureInfo.InvariantCulture));

                    // Wrap UV coordinates to [0, 1] range for mirroring/UDIM workflows (Issue #35).
                    // Only wrap values that are meaningfully outside [0,1] - a genuine UDIM/mirror
                    // offset. Exporters commonly emit tiny negative/>1 noise (e.g. -0.000001) right
                    // at the UV boundary; wrapping THAT via Math.Floor sends it flying to the
                    // opposite edge of the texture (-0.000001 -> 0.999999) instead of leaving it at
                    // ~0 where it belongs, corrupting every downstream edge interpolation that
                    // touches that vertex. Such noise gets clamped back to [0,1] instead.
                    const double uvNoiseEpsilon = 1e-4;
                    if (vtx.X < -uvNoiseEpsilon || vtx.X > 1 + uvNoiseEpsilon ||
                        vtx.Y < -uvNoiseEpsilon || vtx.Y > 1 + uvNoiseEpsilon)
                        vtx = new Vertex2(vtx.X - Math.Floor(vtx.X), vtx.Y - Math.Floor(vtx.Y));
                    else if (vtx.X < 0 || vtx.X > 1 || vtx.Y < 0 || vtx.Y > 1)
                        vtx = new Vertex2(Math.Clamp(vtx.X, 0, 1), Math.Clamp(vtx.Y, 0, 1));

                    textureVertices.Add(vtx);
                    break;
                case "vn" when segs.Length >= 4:
                    // Skipping normals (recognized but not used)
                    break;
                case "usemtl" when segs.Length == 2:
                {
                    if (!materialsDict.ContainsKey(segs[1]))
                        throw new Exception($"Material {segs[1]} not found");

                    currentMaterial = segs[1];
                    break;
                }
                case "f" when segs.Length == 4:
                {
                    var first = segs[1].Split('/');
                    var second = segs[2].Split('/');
                    var third = segs[3].Split('/');

                    var hasTexture = first.Length > 1 && first[1].Length > 0 && second.Length > 1 &&
                                     second[1].Length > 0 && third.Length > 1 && third[1].Length > 0;

                    // We ignore this
                    // var hasNormals = vertexIndices[0][2] != null && vertexIndices[1][2] != null && vertexIndices[2][2] != null;

                    var v1 = int.Parse(first[0]);
                    var v2 = int.Parse(second[0]);
                    var v3 = int.Parse(third[0]);

                    if (hasTexture)
                    {
                        var vt1 = int.Parse(first[1]);
                        var vt2 = int.Parse(second[1]);
                        var vt3 = int.Parse(third[1]);

                        var materialIndex = 0;
                        if (currentMaterial != string.Empty)
                        {
                            materialIndex = materialsDict[currentMaterial];
                        }

                        var faceT = new FaceT(
                            v1 - 1,
                            v2 - 1,
                            v3 - 1,
                            vt1 - 1,
                            vt2 - 1,
                            vt3 - 1,
                            materialIndex);

                        facesT.Add(faceT);
                    }
                    else
                    {
                        var face = new Face(
                            v1 - 1,
                            v2 - 1,
                            v3 - 1);

                        faces.Add(face);
                    }

                    break;
                }
                case "f" when segs.Length >= 5:
                {
                    // Fan triangulation for quads and n-gons (Issue #60)
                    // Splits polygon v0-v1-v2-...-vN into triangles: (v0,v1,v2), (v0,v2,v3), ..., (v0,vN-1,vN)
                    var faceVerts = new string[segs.Length - 1][];
                    for (var fi = 0; fi < segs.Length - 1; fi++)
                        faceVerts[fi] = segs[fi + 1].Split('/');

                    // Determine whether all vertices have texture coordinates.
                    var anyHasTex = false;
                    var allHaveTex = true;
                    for (var fi = 0; fi < faceVerts.Length; fi++)
                    {
                        var hasVt = faceVerts[fi].Length > 1 && faceVerts[fi][1].Length > 0;
                        anyHasTex |= hasVt;
                        allHaveTex &= hasVt;
                    }

                    if (anyHasTex && !allHaveTex)
                        throw new FormatException("OBJ face has inconsistent texture coordinate indices (mixed v/vt and v//vn or missing vt) which is not supported.");

                    var hasTex = allHaveTex;

                    // Parse the pivot vertex once outside the loop
                    var fv0 = int.Parse(faceVerts[0][0]) - 1;
                    var ft0 = hasTex ? int.Parse(faceVerts[0][1]) - 1 : 0;

                    for (var fi = 1; fi < faceVerts.Length - 1; fi++)
                    {
                        var fv1 = int.Parse(faceVerts[fi][0]) - 1;
                        var fv2 = int.Parse(faceVerts[fi + 1][0]) - 1;

                        if (hasTex)
                        {
                            var ft1 = int.Parse(faceVerts[fi][1]) - 1;
                            var ft2 = int.Parse(faceVerts[fi + 1][1]) - 1;

                            var materialIndex = 0;
                            if (currentMaterial != string.Empty)
                                materialIndex = materialsDict[currentMaterial];

                            facesT.Add(new FaceT(fv0, fv1, fv2, ft0, ft1, ft2, materialIndex));
                        }
                        else
                        {
                            faces.Add(new Face(fv0, fv1, fv2));
                        }
                    }

                    break;
                }
                case "mtllib" when segs.Length >= 2:
                {
                    var mtlFileName = string.Join(" ", segs, 1, segs.Length - 1);
                    var mtlFilePath = Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, mtlFileName);

                    var mats = Material.ReadMtl(mtlFilePath, out var mtlDeps, fileName, ignoreNormalMaps);

                    deps.AddRange(mtlDeps);
                    deps.Add(mtlFilePath);

                    foreach (var mat in mats)
                    {
                        materials.Add(mat);
                        materialsDict.Add(mat.Name, materials.Count - 1);
                    }

                    break;
                }
                case "l":
                    // Line elements are irrelevant for triangular meshes, skip gracefully (Issue #64)
                    break;
                case "cstype" or "deg" or "bmat" or "step" or "curv" or "curv2" or "surf" or "parm" or "trim"
                    or "end" or "hole" or "scrv" or "sp" or "con":

                    throw new NotSupportedException("Element not supported: '" + line + "'");
            }
        }

        dependencies = deps.ToArray();

        if (vertexColors.Count > 0 && vertexColors.Count != vertices.Count)
            throw new InvalidDataException(
                $"Vertex color count ({vertexColors.Count}) does not match vertex count ({vertices.Count}). " +
                "All vertices must have colors or none.");

        var colors = vertexColors.Count > 0 ? vertexColors : null;

        return textureVertices.Count != 0
            ? new MeshT(vertices, textureVertices, facesT, materials, colors)
            : new Mesh(vertices, faces, colors);
    }

    #region Splitters

    private static readonly IVertexUtils yutils3 = new VertexUtilsY();
    private static readonly IVertexUtils xutils3 = new VertexUtilsX();
    private static readonly IVertexUtils zutils3 = new VertexUtilsZ();

    // Splits at q as normal when overlap is 0 (the default - no behavior change). When overlap > 0,
    // each side is cut past the nominal boundary instead of exactly at it - left keeps everything up
    // to q+overlap, right keeps everything from q-overlap - so both tiles carry a redundant band of
    // real, duplicated surface straddling the seam. That's twice the splitting work (two full passes
    // over the source mesh instead of one), but it reuses IMesh.Split/CutEdge entirely unchanged.
    private static int SplitWithOverlap(IMesh mesh, IVertexUtils utils, double q, double overlap,
        out IMesh left, out IMesh right)
    {
        if (overlap <= 0)
            return mesh.Split(utils, q, out left, out right);

        var count = mesh.Split(utils, q + overlap, out left, out _);
        count += mesh.Split(utils, q - overlap, out _, out right);
        return count;
    }

    // Deterministic (not System.Random-seeded) string hash so the same tile name always nudges the
    // same way across separate runs/machines, keeping builds reproducible. string.GetHashCode() is
    // randomized per-process in .NET and can't be used for this.
    private static int StableHash(string s)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in s)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }

    // Fraction of the tile's bounding-box diagonal used as the nudge magnitude. Tiles are viewed from a
    // distance proportional to their size, so the depth-buffer resolution the nudge must beat scales with it too.
    internal const double OverlapNudgeTileFraction = 1e-4;

    // Nudges a leaf tile by a small random per-axis offset, seeded deterministically from its own
    // name. Only relevant when overlap > 0: the overlap band's two copies of a boundary surface are
    // otherwise perfectly coincident geometry, which z-fights unpredictably in the viewer. A tiny
    // offset breaks the tie. Magnitude is capped well below the overlap width so it can't reopen the
    // gap the overlap exists to close.
    internal static void ApplyOverlapNudge(IMesh mesh, double overlap)
    {
        if (overlap <= 0) return;

        var magnitude = Math.Min(0.2 * overlap, OverlapNudgeTileFraction * mesh.Bounds.Diagonal());
        var rng = new Random(StableHash(mesh.Name));

        double NextOffset() => (rng.NextDouble() * 2 - 1) * magnitude;

        mesh.Translate(new Vertex3(NextOffset(), NextOffset(), NextOffset()));
    }

    public static async Task<int> RecurseSplitXY(IMesh mesh, int depth, Box3 bounds, ConcurrentBag<IMesh> meshes,
        double overlap = 0.0)
    {
        Debug.WriteLine($"RecurseSplitXY('{mesh.Name}' {mesh.VertexCount}, {depth}, {bounds})");

        if (depth == 0)
        {
            if (mesh.FacesCount > 0)
            {
                ApplyOverlapNudge(mesh, overlap);
                meshes.Add(mesh);
            }
            return 0;
        }

        var center = bounds.Center;

        var count = SplitWithOverlap(mesh, xutils3, center.X, overlap, out var left, out var right);
        count += SplitWithOverlap(left, yutils3, center.Y, overlap, out var topleft, out var topright);
        count += SplitWithOverlap(right, yutils3, center.Y, overlap, out var bottomleft, out var bottomright);

        var xbounds = bounds.Split(Axis.X);
        var ybounds1 = xbounds[0].Split(Axis.Y);
        var ybounds2 = xbounds[1].Split(Axis.Y);

        var nextDepth = depth - 1;

        var tasks = new List<Task<int>>();

        if (topleft.FacesCount > 0) tasks.Add(RecurseSplitXY(topleft, nextDepth, ybounds1[0], meshes, overlap));
        if (bottomleft.FacesCount > 0) tasks.Add(RecurseSplitXY(bottomleft, nextDepth, ybounds2[0], meshes, overlap));
        if (topright.FacesCount > 0) tasks.Add(RecurseSplitXY(topright, nextDepth, ybounds1[1], meshes, overlap));
        if (bottomright.FacesCount > 0) tasks.Add(RecurseSplitXY(bottomright, nextDepth, ybounds2[1], meshes, overlap));

        await Task.WhenAll(tasks);

        return count + tasks.Sum(t => t.Result);
    }

    public static async Task<int> RecurseSplitXY(IMesh mesh, int depth, Func<IMesh, Vertex3> getSplitPoint,
        ConcurrentBag<IMesh> meshes, double overlap = 0.0)
    {
        var center = getSplitPoint(mesh);

        var count = SplitWithOverlap(mesh, xutils3, center.X, overlap, out var left, out var right);
        count += SplitWithOverlap(left, yutils3, center.Y, overlap, out var topleft, out var bottomleft);
        count += SplitWithOverlap(right, yutils3, center.Y, overlap, out var topright, out var bottomright);

        var nextDepth = depth - 1;

        if (nextDepth == 0)
        {
            if (topleft.FacesCount > 0) { ApplyOverlapNudge(topleft, overlap); meshes.Add(topleft); }
            if (bottomleft.FacesCount > 0) { ApplyOverlapNudge(bottomleft, overlap); meshes.Add(bottomleft); }
            if (topright.FacesCount > 0) { ApplyOverlapNudge(topright, overlap); meshes.Add(topright); }
            if (bottomright.FacesCount > 0) { ApplyOverlapNudge(bottomright, overlap); meshes.Add(bottomright); }

            return count;
        }

        var tasks = new List<Task<int>>();

        if (topleft.FacesCount > 0) tasks.Add(RecurseSplitXY(topleft, nextDepth, getSplitPoint, meshes, overlap));
        if (bottomleft.FacesCount > 0) tasks.Add(RecurseSplitXY(bottomleft, nextDepth, getSplitPoint, meshes, overlap));
        if (topright.FacesCount > 0) tasks.Add(RecurseSplitXY(topright, nextDepth, getSplitPoint, meshes, overlap));
        if (bottomright.FacesCount > 0) tasks.Add(RecurseSplitXY(bottomright, nextDepth, getSplitPoint, meshes, overlap));

        await Task.WhenAll(tasks);

        return count + tasks.Sum(t => t.Result);
    }

    public static async Task<int> RecurseSplitXYZ(IMesh mesh, int depth, Func<IMesh, Vertex3> getSplitPoint,
        ConcurrentBag<IMesh> meshes, double overlap = 0.0)
    {
        var center = getSplitPoint(mesh);

        var count = SplitWithOverlap(mesh, xutils3, center.X, overlap, out var left, out var right);
        count += SplitWithOverlap(left, yutils3, center.Y, overlap, out var topleft, out var bottomleft);
        count += SplitWithOverlap(right, yutils3, center.Y, overlap, out var topright, out var bottomright);

        count += SplitWithOverlap(topleft, zutils3, center.Z, overlap, out var topleftnear, out var topleftfar);
        count += SplitWithOverlap(bottomleft, zutils3, center.Z, overlap, out var bottomleftnear, out var bottomleftfar);

        count += SplitWithOverlap(topright, zutils3, center.Z, overlap, out var toprightnear, out var toprightfar);
        count += SplitWithOverlap(bottomright, zutils3, center.Z, overlap, out var bottomrightnear, out var bottomrightfar);

        var nextDepth = depth - 1;

        if (nextDepth == 0)
        {
            if (topleftnear.FacesCount > 0) { ApplyOverlapNudge(topleftnear, overlap); meshes.Add(topleftnear); }
            if (topleftfar.FacesCount > 0) { ApplyOverlapNudge(topleftfar, overlap); meshes.Add(topleftfar); }
            if (bottomleftnear.FacesCount > 0) { ApplyOverlapNudge(bottomleftnear, overlap); meshes.Add(bottomleftnear); }
            if (bottomleftfar.FacesCount > 0) { ApplyOverlapNudge(bottomleftfar, overlap); meshes.Add(bottomleftfar); }

            if (toprightnear.FacesCount > 0) { ApplyOverlapNudge(toprightnear, overlap); meshes.Add(toprightnear); }
            if (toprightfar.FacesCount > 0) { ApplyOverlapNudge(toprightfar, overlap); meshes.Add(toprightfar); }
            if (bottomrightnear.FacesCount > 0) { ApplyOverlapNudge(bottomrightnear, overlap); meshes.Add(bottomrightnear); }
            if (bottomrightfar.FacesCount > 0) { ApplyOverlapNudge(bottomrightfar, overlap); meshes.Add(bottomrightfar); }

            return count;
        }

        var tasks = new List<Task<int>>();

        if (topleftnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(topleftnear, nextDepth, getSplitPoint, meshes, overlap));
        if (topleftfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(topleftfar, nextDepth, getSplitPoint, meshes, overlap));
        if (bottomleftnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomleftnear, nextDepth, getSplitPoint, meshes, overlap));
        if (bottomleftfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomleftfar, nextDepth, getSplitPoint, meshes, overlap));

        if (toprightnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(toprightnear, nextDepth, getSplitPoint, meshes, overlap));
        if (toprightfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(toprightfar, nextDepth, getSplitPoint, meshes, overlap));
        if (bottomrightnear.FacesCount > 0)
            tasks.Add(RecurseSplitXYZ(bottomrightnear, nextDepth, getSplitPoint, meshes, overlap));
        if (bottomrightfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomrightfar, nextDepth, getSplitPoint, meshes, overlap));

        await Task.WhenAll(tasks);

        return count + tasks.Sum(t => t.Result);
    }

    public static async Task<int> RecurseSplitXYZ(IMesh mesh, int depth, Box3 bounds, ConcurrentBag<IMesh> meshes,
        double overlap = 0.0)
    {
        Debug.WriteLine($"RecurseSplitXYZ('{mesh.Name}' {mesh.VertexCount}, {depth}, {bounds})");

        if (depth == 0)
        {
            if (mesh.FacesCount > 0)
            {
                ApplyOverlapNudge(mesh, overlap);
                meshes.Add(mesh);
            }
            return 0;
        }

        var center = bounds.Center;

        var count = SplitWithOverlap(mesh, xutils3, center.X, overlap, out var left, out var right);
        count += SplitWithOverlap(left, yutils3, center.Y, overlap, out var topleft, out var bottomleft);
        count += SplitWithOverlap(right, yutils3, center.Y, overlap, out var topright, out var bottomright);

        count += SplitWithOverlap(topleft, zutils3, center.Z, overlap, out var topleftnear, out var topleftfar);
        count += SplitWithOverlap(bottomleft, zutils3, center.Z, overlap, out var bottomleftnear, out var bottomleftfar);

        count += SplitWithOverlap(topright, zutils3, center.Z, overlap, out var toprightnear, out var toprightfar);
        count += SplitWithOverlap(bottomright, zutils3, center.Z, overlap, out var bottomrightnear, out var bottomrightfar);

        var xbounds = bounds.Split(Axis.X);
        var ybounds1 = xbounds[0].Split(Axis.Y);
        var ybounds2 = xbounds[1].Split(Axis.Y);

        var zbounds1 = ybounds1[0].Split(Axis.Z);
        var zbounds2 = ybounds1[1].Split(Axis.Z);

        var zbounds3 = ybounds2[0].Split(Axis.Z);
        var zbounds4 = ybounds2[1].Split(Axis.Z);

        var nextDepth = depth - 1;

        var tasks = new List<Task<int>>();

        if (topleftnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(topleftnear, nextDepth, zbounds1[0], meshes, overlap));
        if (topleftfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(topleftfar, nextDepth, zbounds1[1], meshes, overlap));
        if (bottomleftnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomleftnear, nextDepth, zbounds2[0], meshes, overlap));
        if (bottomleftfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomleftfar, nextDepth, zbounds2[1], meshes, overlap));
        if (toprightnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(toprightnear, nextDepth, zbounds3[0], meshes, overlap));
        if (toprightfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(toprightfar, nextDepth, zbounds3[1], meshes, overlap));
        if (bottomrightnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomrightnear, nextDepth, zbounds4[0], meshes, overlap));
        if (bottomrightfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomrightfar, nextDepth, zbounds4[1], meshes, overlap));

        await Task.WhenAll(tasks);

        return count + tasks.Sum(t => t.Result);
    }

    /// <summary>
    /// Recursive balanced split on XY — computes split point per-sub-mesh per-axis
    /// for optimal vertex balancing.
    /// </summary>
    public static async Task<int> RecurseSplitXYBalanced(IMesh mesh, int depth,
        Func<IMesh, Vertex3> getSplitPoint, ConcurrentBag<IMesh> meshes, double overlap = 0.0)
    {
        var splitX = getSplitPoint(mesh);

        var count = SplitWithOverlap(mesh, xutils3, splitX.X, overlap, out var left, out var right);

        // Ricalcola il punto di split Y per ogni sotto-mesh separatamente
        if (left.FacesCount > 0)
        {
            var splitYLeft = getSplitPoint(left);
            count += SplitWithOverlap(left, yutils3, splitYLeft.Y, overlap, out var topleft, out var bottomleft);

            if (depth <= 1)
            {
                if (topleft.FacesCount > 0) { ApplyOverlapNudge(topleft, overlap); meshes.Add(topleft); }
                if (bottomleft.FacesCount > 0) { ApplyOverlapNudge(bottomleft, overlap); meshes.Add(bottomleft); }
            }
            else
            {
                var nextDepth = depth - 1;
                var tasks = new List<Task<int>>();
                if (topleft.FacesCount > 0)
                    tasks.Add(RecurseSplitXYBalanced(topleft, nextDepth, getSplitPoint, meshes, overlap));
                if (bottomleft.FacesCount > 0)
                    tasks.Add(RecurseSplitXYBalanced(bottomleft, nextDepth, getSplitPoint, meshes, overlap));
                await Task.WhenAll(tasks);
                count += tasks.Sum(t => t.Result);
            }
        }

        if (right.FacesCount > 0)
        {
            var splitYRight = getSplitPoint(right);
            count += SplitWithOverlap(right, yutils3, splitYRight.Y, overlap, out var topright, out var bottomright);

            if (depth <= 1)
            {
                if (topright.FacesCount > 0) { ApplyOverlapNudge(topright, overlap); meshes.Add(topright); }
                if (bottomright.FacesCount > 0) { ApplyOverlapNudge(bottomright, overlap); meshes.Add(bottomright); }
            }
            else
            {
                var nextDepth = depth - 1;
                var tasks = new List<Task<int>>();
                if (topright.FacesCount > 0)
                    tasks.Add(RecurseSplitXYBalanced(topright, nextDepth, getSplitPoint, meshes, overlap));
                if (bottomright.FacesCount > 0)
                    tasks.Add(RecurseSplitXYBalanced(bottomright, nextDepth, getSplitPoint, meshes, overlap));
                await Task.WhenAll(tasks);
                count += tasks.Sum(t => t.Result);
            }
        }

        return count;
    }

    /// <summary>
    /// Recursive balanced split on XYZ — computes split point per-sub-mesh per-axis.
    /// </summary>
    public static async Task<int> RecurseSplitXYZBalanced(IMesh mesh, int depth,
        Func<IMesh, Vertex3> getSplitPoint, ConcurrentBag<IMesh> meshes, double overlap = 0.0)
    {
        var splitX = getSplitPoint(mesh);
        var count = SplitWithOverlap(mesh, xutils3, splitX.X, overlap, out var left, out var right);

        // Per ogni metà X, ricalcola Y
        var halves = new[] { left, right };
        var quadrants = new List<IMesh>();

        foreach (var half in halves)
        {
            if (half.FacesCount == 0) continue;
            var splitY = getSplitPoint(half);
            count += SplitWithOverlap(half, yutils3, splitY.Y, overlap, out var top, out var bottom);
            if (top.FacesCount > 0) quadrants.Add(top);
            if (bottom.FacesCount > 0) quadrants.Add(bottom);
        }

        // Per ogni quadrante XY, ricalcola Z
        var octants = new List<IMesh>();
        foreach (var quad in quadrants)
        {
            var splitZ = getSplitPoint(quad);
            count += SplitWithOverlap(quad, zutils3, splitZ.Z, overlap, out var near, out var far);
            if (near.FacesCount > 0) octants.Add(near);
            if (far.FacesCount > 0) octants.Add(far);
        }

        var nextDepth = depth - 1;

        if (nextDepth == 0)
        {
            foreach (var oct in octants)
            {
                ApplyOverlapNudge(oct, overlap);
                meshes.Add(oct);
            }
            return count;
        }

        var tasks = octants
            .Select(oct => RecurseSplitXYZBalanced(oct, nextDepth, getSplitPoint, meshes, overlap))
            .ToList();

        await Task.WhenAll(tasks);
        return count + tasks.Sum(t => t.Result);
    }

    #endregion
}