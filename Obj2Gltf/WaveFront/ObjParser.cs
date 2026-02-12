using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SilentWave.Obj2Gltf.Geom;

namespace SilentWave.Obj2Gltf.WaveFront
{
    /// <summary>
    /// parse an obj file
    /// </summary>
    public class ObjParser
    {
        private static class Statements
        {
            public const string Comment = "#";
            public const string MtlLib = "mtllib";
            public const string Vertex = "v";
            public const string VectorNormal = "vn";
            public const string VectorTexture = "vt";
            public const string Group = "g";
            public const string UseMaterial = "usemtl";
            public const string Face = "f";
        }


        private static FaceVertex GetVertex(string vStr)
        {
            var v1Str = vStr.Split('/');
            if (v1Str.Length >= 3)
            {
                var v = int.Parse(v1Str[0]);
                var t = 0;
                if (!string.IsNullOrEmpty(v1Str[1]))
                {
                    t = int.Parse(v1Str[1]);
                }
                var n = int.Parse(v1Str[2]);
                return new FaceVertex(v, t, n);
            }
            else if (v1Str.Length >= 2)
            {
                return new FaceVertex(int.Parse(v1Str[0]), int.Parse(v1Str[1]), 0);
            }
            return new FaceVertex(int.Parse(v1Str[0]));
        }

        private static string[] SplitLine(string line)
        {
            return line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool StartWith(string line, string str)
        {
            return line.StartsWith(str) && (line[str.Length] == ' ' || line[str.Length] == '\t');
        }
        /// <summary>
        /// get parsed obj model
        /// </summary>
        /// <returns></returns>
        public ObjModel Parse(string objFilePath, bool removeDegenerateFaces = false, Encoding encoding = null)
        {
            if (string.IsNullOrEmpty(objFilePath)) throw new ArgumentNullException(nameof(objFilePath));

            var model = new ObjModel
            {
                Name = Path.GetFileNameWithoutExtension(objFilePath)
            };
            var _reader = encoding != null
               ? new StreamReader(objFilePath, encoding)
               : new StreamReader(objFilePath);

            using (_reader)
            {
                model.Materials.Add(new Material() { Ambient = new Reflectivity(new FactorColor(1)) });
                var currentMaterialName = "default";
                var currentGeometries = model.GetOrAddGeometries("default");
                Face currentFace = null;

                while (!_reader.EndOfStream)
                {
                    var line = _reader.ReadLine().Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith(Statements.Comment)) continue;
                    if (StartWith(line, Statements.MtlLib))
                    {
                        model.MatFilename = line.Substring(6).Trim();
                    }
                    else if (StartWith(line, Statements.Vertex))
                    {
                        var vStr = line.Substring(2).Trim();
                        var strs = SplitLine(vStr);
                        var v = new SVec3(
                            float.Parse(strs[0], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat),
                            float.Parse(strs[1], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat),
                            float.Parse(strs[2], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat));
                        model.Vertices.Add(v);

                        if (strs.Length >= 6)
                        {
                            var c = new SVec3(
                                float.Parse(strs[3], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat),
                                float.Parse(strs[4], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat),
                                float.Parse(strs[5], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat));
                            model.Colors.Add(c);
                        }
                    }
                    else if (StartWith(line, Statements.VectorNormal))
                    {
                        var vnStr = line.Substring(3).Trim();
                        var strs = SplitLine(vnStr);
                        var vn = new SVec3(
                            float.Parse(strs[0], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat),
                            float.Parse(strs[1], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat),
                            float.Parse(strs[2], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat));
                        model.Normals.Add(vn);
                    }
                    else if (StartWith(line, Statements.VectorTexture))
                    {
                        var vtStr = line.Substring(3).Trim();
                        var strs = SplitLine(vtStr);
                        var vt = new SVec2(
                            float.Parse(strs[0], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat),
                            float.Parse(strs[1], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat));
                        model.Uvs.Add(vt);
                    }
                    else if (StartWith(line, Statements.Group))
                    {
                        var gStr = line.Substring(2);
                        var groupNames = SplitLine(gStr);

                        currentGeometries = model.GetOrAddGeometries(groupNames);
                    }
                    else if (StartWith(line, Statements.UseMaterial))
                    {
                        var umtl = line.Substring(7).Trim();
                        currentMaterialName = umtl;
                    }
                    else if (StartWith(line, Statements.Face))
                    {
                        var fStr = line.Substring(2).Trim();
                        currentFace = new Face(currentMaterialName);
                        var strs = SplitLine(fStr);
                        if (strs.Length < 3) continue; // ignore face that has less than 3 vertices
                        if (strs.Length == 3)
                        {
                            var v1 = GetVertex(strs[0]);
                            var v2 = GetVertex(strs[1]);
                            var v3 = GetVertex(strs[2]);
                            var f = new FaceTriangle(v1, v2, v3);
                            currentFace.Triangles.Add(f);
                        }
                        else if (strs.Length == 4)
                        {
                            var v1 = GetVertex(strs[0]);
                            var v2 = GetVertex(strs[1]);
                            var v3 = GetVertex(strs[2]);
                            var f = new FaceTriangle(v1, v2, v3);
                            currentFace.Triangles.Add(f);
                            var v4 = GetVertex(strs[3]);
                            var ff = new FaceTriangle(v1, v3, v4);
                            currentFace.Triangles.Add(ff);
                        }
                        else //if (strs.Length > 4)
                        {
                            var points = new List<SVec3>();
                            for (var i = 0; i < strs.Length; i++)
                            {
                                var vv = GetVertex(strs[i]);
                                var p = model.Vertices[vv.V - 1];
                                points.Add(p);
                            }
                            var planeAxis = GeomUtil.ComputeProjectTo2DArguments(points);
                            if (planeAxis != null)
                            {
                                var points2D = GeomUtil.CreateProjectPointsTo2DFunction(planeAxis, points);
                                var indices = PolygonPipeline.Triangulate(points2D, null);
                                if (indices.Length == 0)
                                {
                                    // TODO:
                                }
                                for (var i = 0; i < indices.Length - 2; i += 3)
                                {
                                    var vv1 = GetVertex(strs[indices[i]]);
                                    var vv2 = GetVertex(strs[indices[i + 1]]);
                                    var vv3 = GetVertex(strs[indices[i + 2]]);
                                    var ff = new FaceTriangle(vv1, vv2, vv3);
                                    currentFace.Triangles.Add(ff);
                                }
                            }
                            else
                            {
                                // TODO:
                            }
                        }
                        foreach (var currentGeometry in currentGeometries)
                        {
                            currentGeometry.Faces.Add(currentFace);
                        }
                    }
                    else
                    {
                        //var strs = SplitLine(line);
                    }
                }
                if (removeDegenerateFaces)
                {
                    foreach (var geom in model.Geometries)
                    {
                        foreach (var face in geom.Faces)
                        {
                            var notDegradedTriangles = new List<FaceTriangle>();
                            for (int i = 0; i < face.Triangles.Count; i++)
                            {
                                var triangle = face.Triangles[i];
                                var a = model.Vertices[triangle.V1.V - 1];
                                var b = model.Vertices[triangle.V2.V - 1];
                                var c = model.Vertices[triangle.V3.V - 1];
                                var sideLengths = new List<float>() {
                                    (a - b).GetLength(),
                                    (b - c).GetLength(),
                                    (c - a).GetLength()
                                };
                                sideLengths.Sort();
                                if (!(sideLengths[0] + sideLengths[1] <= sideLengths[2]))
                                    notDegradedTriangles.Add(triangle);
                            }
                            face.Triangles = notDegradedTriangles;
                        }
                    }

                }
            }
            return model;
        }
    }
}
