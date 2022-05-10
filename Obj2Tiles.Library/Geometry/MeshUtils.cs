using System.Collections.Concurrent;
using System.Globalization;
using Obj2Tiles.Library.Materials;

namespace Obj2Tiles.Library.Geometry;

public class MeshUtils
{
    private static readonly CultureInfo en = CultureInfo.GetCultureInfo("en-US");

    public static IMesh LoadMesh(string fileName)
    {
        using var reader = new StreamReader(fileName);

        var vertices = new List<Vertex3>();
        var textureVertices = new List<Vertex2>();
        var facesT = new List<FaceT>();
        var faces = new List<Face>();
        var materials = new List<Material>();
        var materialsDict = new Dictionary<string, int>();
        var currentMaterial = string.Empty;

        while (true)
        {
            var line = reader.ReadLine();

            if (line == null) break;

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var segs = line.Split(' ');

            switch (segs[0])
            {
                case "v" when segs.Length >= 4:
                    vertices.Add(new Vertex3(
                        double.Parse(segs[1], en),
                        double.Parse(segs[2], en),
                        double.Parse(segs[3], en)));
                    break;
                case "vt" when segs.Length >= 3:
                    textureVertices.Add(new Vertex2(
                        double.Parse(segs[1], en),
                        double.Parse(segs[2], en)));
                    break;
                case "vn" when segs.Length == 3:
                    // Skipping normals
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

                    var hasTexture = first[1].Length > 0 && second[1].Length > 0 && third[1].Length > 0;

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

                        var faceT = new FaceT(
                            v1 - 1,
                            v2 - 1,
                            v3 - 1,
                            vt1 - 1,
                            vt2 - 1,
                            vt3 - 1,
                            materialsDict[currentMaterial]);

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
                case "mtllib" when segs.Length == 2:
                {
                    var mtlFileName = segs[1];
                    var mtlFilePath = Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, mtlFileName);

                    var mats = Material.ReadMtl(mtlFilePath);

                    foreach (var mat in mats)
                    {
                        materials.Add(mat);
                        materialsDict.Add(mat.Name, materials.Count - 1);
                    }

                    break;
                }
                case "l" or "cstype" or "deg" or "bmat" or "step" or "curv" or "curv2" or "surf" or "parm" or "trim"
                    or "end" or "hole" or "scrv" or "sp" or "con":

                    throw new NotSupportedException("Element not supported: '" + line + "'");
            }
        }

        return textureVertices.Any()
            ? new MeshT(vertices, textureVertices, facesT, materials)
            : new Mesh(vertices, faces);
    }

    #region Splitters

    private static readonly IVertexUtils yutils3 = new VertexUtilsY();
    private static readonly IVertexUtils xutils3 = new VertexUtilsX();
    private static readonly IVertexUtils zutils3 = new VertexUtilsZ();

    public static async Task<int> RecurseSplitXY(IMesh mesh, int depth, Box3 bounds, ConcurrentBag<IMesh> meshes)
    {
        var center = bounds.Center; 

        var count = mesh.Split(xutils3, center.X, out var left, out var right);
        count += left.Split(yutils3, center.Y, out var topleft, out var bottomleft);
        count += right.Split(yutils3, center.Y, out var topright, out var bottomright);

        var nextDepth = depth - 1;

        if (nextDepth == 0)
        {
            if (topleft.FacesCount > 0)
                meshes.Add(topleft);

            if (bottomleft.FacesCount > 0) meshes.Add(bottomleft);
            if (topright.FacesCount > 0) meshes.Add(topright);
            if (bottomright.FacesCount > 0) meshes.Add(bottomright);

            return count;
        }

        var tasks = new List<Task<int>>();

        if (topleft.FacesCount > 0)
        {        
            var topleftBounds = new Box3(bounds.Min, new Vertex3(center.X, center.Y, bounds.Max.Z));
            tasks.Add(RecurseSplitXY(topleft, nextDepth, topleftBounds, meshes));
        }

        if (bottomleft.FacesCount > 0)
        {
            var bottomleftBounds = new Box3(new Vertex3(center.X, bounds.Min.Y, center.Z), new Vertex3(bounds.Max.X, center.Y, bounds.Max.Z));
            tasks.Add(RecurseSplitXY(bottomleft, nextDepth, bottomleftBounds, meshes));
        }

        if (topright.FacesCount > 0)
        {
            var toprightBounds = new Box3(new Vertex3(center.X, bounds.Min.Y, center.Z), new Vertex3(bounds.Max.X, center.Y, bounds.Max.Z));
            tasks.Add(RecurseSplitXY(topright, nextDepth, toprightBounds, meshes));
        }

        if (bottomright.FacesCount > 0)
        {
            var bottomrightBounds = new Box3(new Vertex3(center.X, bounds.Min.Y, center.Z), new Vertex3(bounds.Max.X, center.Y, bounds.Max.Z));
            tasks.Add(RecurseSplitXY(bottomright, nextDepth, bottomrightBounds, meshes));
        }

        await Task.WhenAll(tasks);

        return count + tasks.Sum(t => t.Result);
    }
    
    public static async Task<int> RecurseSplitXY(IMesh mesh, int depth, Func<IMesh, Vertex3> getSplitPoint,
        ConcurrentBag<IMesh> meshes)
    {
        var center = getSplitPoint(mesh); //mesh.GetVertexBaricenter();

        var count = mesh.Split(xutils3, center.X, out var left, out var right);
        count += left.Split(yutils3, center.Y, out var topleft, out var bottomleft);
        count += right.Split(yutils3, center.Y, out var topright, out var bottomright);

        var nextDepth = depth - 1;

        if (nextDepth == 0)
        {
            if (topleft.FacesCount > 0)
                meshes.Add(topleft);

            if (bottomleft.FacesCount > 0) meshes.Add(bottomleft);
            if (topright.FacesCount > 0) meshes.Add(topright);
            if (bottomright.FacesCount > 0) meshes.Add(bottomright);

            return count;
        }

        var tasks = new List<Task<int>>();

        if (topleft.FacesCount > 0) tasks.Add(RecurseSplitXY(topleft, nextDepth, getSplitPoint, meshes));
        if (bottomleft.FacesCount > 0) tasks.Add(RecurseSplitXY(bottomleft, nextDepth, getSplitPoint, meshes));
        if (topright.FacesCount > 0) tasks.Add(RecurseSplitXY(topright, nextDepth, getSplitPoint, meshes));
        if (bottomright.FacesCount > 0) tasks.Add(RecurseSplitXY(bottomright, nextDepth, getSplitPoint, meshes));

        await Task.WhenAll(tasks);

        return count + tasks.Sum(t => t.Result);
    }

    public static async Task<int> RecurseSplitXYZ(IMesh mesh, int depth, Func<IMesh, Vertex3> getSplitPoint,
        ConcurrentBag<IMesh> meshes)
    {
        var center = getSplitPoint(mesh);

        var count = mesh.Split(xutils3, center.X, out var left, out var right);
        count += left.Split(yutils3, center.Y, out var topleft, out var bottomleft);
        count += right.Split(yutils3, center.Y, out var topright, out var bottomright);

        count += topleft.Split(zutils3, center.Z, out var topleftnear, out var topleftfar);
        count += bottomleft.Split(zutils3, center.Z, out var bottomleftnear, out var bottomleftfar);

        count += topright.Split(zutils3, center.Z, out var toprightnear, out var toprightfar);
        count += bottomright.Split(zutils3, center.Z, out var bottomrightnear, out var bottomrightfar);

        var nextDepth = depth - 1;

        if (nextDepth == 0)
        {
            if (topleftnear.FacesCount > 0) meshes.Add(topleftnear);
            if (topleftfar.FacesCount > 0) meshes.Add(topleftfar);
            if (bottomleftnear.FacesCount > 0) meshes.Add(bottomleftnear);
            if (bottomleftfar.FacesCount > 0) meshes.Add(bottomleftfar);

            if (toprightnear.FacesCount > 0) meshes.Add(toprightnear);
            if (toprightfar.FacesCount > 0) meshes.Add(toprightfar);
            if (bottomrightnear.FacesCount > 0) meshes.Add(bottomrightnear);
            if (bottomrightfar.FacesCount > 0) meshes.Add(bottomrightfar);

            return count;
        }

        var tasks = new List<Task<int>>();

        if (topleftnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(topleftnear, nextDepth, getSplitPoint, meshes));
        if (topleftfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(topleftfar, nextDepth, getSplitPoint, meshes));
        if (bottomleftnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomleftnear, nextDepth, getSplitPoint, meshes));
        if (bottomleftfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomleftfar, nextDepth, getSplitPoint, meshes));

        if (toprightnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(toprightnear, nextDepth, getSplitPoint, meshes));
        if (toprightfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(toprightfar, nextDepth, getSplitPoint, meshes));
        if (bottomrightnear.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomrightnear, nextDepth, getSplitPoint, meshes));
        if (bottomrightfar.FacesCount > 0) tasks.Add(RecurseSplitXYZ(bottomrightfar, nextDepth, getSplitPoint, meshes));

        await Task.WhenAll(tasks);

        return count + tasks.Sum(t => t.Result);
    }
    
        public static async Task<int> RecurseSplitXYZ(IMesh mesh, int depth, Box3 bounds,
        ConcurrentBag<IMesh> meshes)
        {
            var center = bounds.Center;

        var count = mesh.Split(xutils3, center.X, out var left, out var right);
        count += left.Split(yutils3, center.Y, out var topleft, out var bottomleft);
        count += right.Split(yutils3, center.Y, out var topright, out var bottomright);

        count += topleft.Split(zutils3, center.Z, out var topleftnear, out var topleftfar);
        count += bottomleft.Split(zutils3, center.Z, out var bottomleftnear, out var bottomleftfar);

        count += topright.Split(zutils3, center.Z, out var toprightnear, out var toprightfar);
        count += bottomright.Split(zutils3, center.Z, out var bottomrightnear, out var bottomrightfar);

        var nextDepth = depth - 1;

        if (nextDepth == 0)
        {
            if (topleftnear.FacesCount > 0) meshes.Add(topleftnear);
            if (topleftfar.FacesCount > 0) meshes.Add(topleftfar);
            if (bottomleftnear.FacesCount > 0) meshes.Add(bottomleftnear);
            if (bottomleftfar.FacesCount > 0) meshes.Add(bottomleftfar);

            if (toprightnear.FacesCount > 0) meshes.Add(toprightnear);
            if (toprightfar.FacesCount > 0) meshes.Add(toprightfar);
            if (bottomrightnear.FacesCount > 0) meshes.Add(bottomrightnear);
            if (bottomrightfar.FacesCount > 0) meshes.Add(bottomrightfar);

            return count;
        }

        var tasks = new List<Task<int>>();

        if (topleftnear.FacesCount > 0)
        {        
            var topleftnearbounds = new Box3(bounds.Min, new Vertex3(center.X, center.Y, center.Z));
            tasks.Add(RecurseSplitXYZ(topleftnear, nextDepth, topleftnearbounds, meshes));
        }

        if (topleftfar.FacesCount > 0)
        {
            var topleftfarbounds = new Box3(new Vertex3(center.X, center.Y, center.Z), bounds.Max);
            tasks.Add(RecurseSplitXYZ(topleftfar, nextDepth, topleftfarbounds, meshes));
        }

        if (bottomleftnear.FacesCount > 0)
        {
            var bottomleftnearbounds = new Box3(bounds.Min, new Vertex3(center.X, center.Y, center.Z));
            tasks.Add(RecurseSplitXYZ(bottomleftnear, nextDepth, bottomleftnearbounds, meshes));
        }

        if (bottomleftfar.FacesCount > 0)
        {
            var bottomleftfarbounds = new Box3(new Vertex3(center.X, center.Y, center.Z), bounds.Max);
            tasks.Add(RecurseSplitXYZ(bottomleftfar, nextDepth, bottomleftfarbounds, meshes));
        }

        if (toprightnear.FacesCount > 0)
        {
            var toprightnearbounds = new Box3(bounds.Min, new Vertex3(center.X, center.Y, center.Z));
            tasks.Add(RecurseSplitXYZ(toprightnear, nextDepth, toprightnearbounds, meshes));
        }

        if (toprightfar.FacesCount > 0)
        {
            var toprightfarbounds = new Box3(new Vertex3(center.X, center.Y, center.Z), bounds.Max);
            tasks.Add(RecurseSplitXYZ(toprightfar, nextDepth, toprightfarbounds, meshes));
        }

        if (bottomrightnear.FacesCount > 0)
        {
            var bottomrightnearbounds = new Box3(bounds.Min, new Vertex3(center.X, center.Y, center.Z));
            tasks.Add(RecurseSplitXYZ(bottomrightnear, nextDepth, bottomrightnearbounds, meshes));
        }

        if (bottomrightfar.FacesCount > 0)
        {
            var bottomrightfarbounds = new Box3(new Vertex3(center.X, center.Y, center.Z), bounds.Max);
            tasks.Add(RecurseSplitXYZ(bottomrightfar, nextDepth, bottomrightfarbounds, meshes));
        }

        await Task.WhenAll(tasks);

        return count + tasks.Sum(t => t.Result);
    }


    #endregion
}