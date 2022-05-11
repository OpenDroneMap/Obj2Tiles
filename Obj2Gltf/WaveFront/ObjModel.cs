using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace SilentWave.Obj2Gltf.WaveFront
{
    /// <summary>
    ///  represents an obj file model
    /// </summary>
    public class ObjModel
    {
        public String Name { get; set; }
        /// <summary>
        /// obj used mat file path
        /// </summary>
        public String MatFilename { get; set; }
        /// <summary>
        /// vertices coordinates list
        /// </summary>
        public List<SVec3> Vertices { get; set; } = new List<SVec3>();
        /// <summary>
        /// vertices normal list
        /// </summary>
        public List<SVec3> Normals { get; set; } = new List<SVec3>();
        /// <summary>
        /// vertices texture coordinates list
        /// </summary>
        public List<SVec2> Uvs { get; set; } = new List<SVec2>();

        private Dictionary<String, Geometry> _Geometries = new Dictionary<String, Geometry>();
        /// <summary>
        /// grouped geometries
        /// </summary>
        public IEnumerable<Geometry> Geometries => _Geometries.Values;
        /// <summary>
        /// mat list from mat file
        /// </summary>
        public List<Material> Materials { get; set; } = new List<Material>();
        /// <summary>
        /// write obj file
        /// </summary>
        /// <param name="writer"></param>
        public void Write(StreamWriter writer)
        {
            if (!String.IsNullOrEmpty(MatFilename))
            {
                writer.WriteLine($"mtllib {MatFilename}");
            }
            foreach (var v in Vertices) writer.WriteLine($"v {v.X} {v.Y} {v.Z}");
            foreach (var t in Uvs) writer.WriteLine($"vt {t.U} {t.V}");
            foreach (var n in Normals) writer.WriteLine($"vn {n.X} {n.Y} {n.Z}");
            foreach (var g in Geometries) g.Write(writer);
        }

        private static FaceVertex GetVertex(FaceVertex v, Dictionary<Int32, Int32> pnts,
            Dictionary<Int32, Int32> normals, Dictionary<Int32, Int32> uvs)
        {
            var v1p = v.V;
            var v1n = v.N;
            var v1t = v.T;
            if (v1p > 0)
            {
                v1p = pnts[v1p];
            }
            if (v1n > 0)
            {
                v1n = normals[v1n];
            }
            if (v1t > 0)
            {
                v1t = uvs[v1t];
            }
            return new FaceVertex(v1p, v1t, v1n);
        }

        private Geometry AddGeo(Geometry g, GeomBox box,
            List<Int32> pnts, List<Int32> normals, List<Int32> uvs)
        {
            var gg = new Geometry(g.Id);

            var pntList = box.Pnts; // new List<int>(); // 
            var normList = box.Norms; // new List<int>(); // 
            var uvList = box.Uvs; // new List<int>(); // 

            //if (pntList.Count == 0)
            //{
            //    foreach (var f in g.Faces)
            //    {
            //        foreach (var t in f.Triangles)
            //        {
            //            var v1 = t.V1;
            //            if (!pntList.Contains(v1.V))
            //            {
            //                pntList.Add(v1.V);
            //            }
            //            if (v1.N > 0 && !normList.Contains(v1.N))
            //            {
            //                normList.Add(v1.N);
            //            }
            //            if (v1.T > 0 && !uvList.Contains(v1.T))
            //            {
            //                uvList.Add(v1.T);
            //            }
            //            var v2 = t.V2;
            //            if (!pntList.Contains(v2.V))
            //            {
            //                pntList.Add(v2.V);
            //            }
            //            if (v2.N > 0 && !normList.Contains(v2.N))
            //            {
            //                normList.Add(v2.N);
            //            }
            //            if (v2.T > 0 && !uvList.Contains(v2.T))
            //            {
            //                uvList.Add(v2.T);
            //            }
            //            var v3 = t.V3;
            //            if (!pntList.Contains(v3.V))
            //            {
            //                pntList.Add(v3.V);
            //            }
            //            if (v3.N > 0 && !normList.Contains(v3.N))
            //            {
            //                normList.Add(v3.N);
            //            }
            //            if (v3.T > 0 && !uvList.Contains(v3.T))
            //            {
            //                uvList.Add(v3.T);
            //            }
            //        }

            //    }
            //}


            var pntDict = new Dictionary<Int32, Int32>();
            var normDict = new Dictionary<Int32, Int32>();
            var uvDict = new Dictionary<Int32, Int32>();

            foreach (var p in pntList)
            {
                var index = pnts.IndexOf(p);
                if (index == -1)
                {
                    index = pnts.Count;
                    pnts.Add(p);
                }
                pntDict.Add(p, index + 1);
            }

            foreach (var n in normList)
            {
                var index = normals.IndexOf(n);
                if (index == -1)
                {
                    index = normals.Count;
                    normals.Add(n);
                }
                normDict.Add(n, index + 1);
            }

            foreach (var t in uvList)
            {
                var index = uvs.IndexOf(t);
                if (index == -1)
                {
                    index = uvs.Count;
                    uvs.Add(t);
                }
                uvDict.Add(t, index + 1);
            }

            foreach (var f in g.Faces)
            {
                var ff = new Face(f.MatName);
                foreach (var t in f.Triangles)
                {
                    var v1 = GetVertex(t.V1, pntDict, normDict, uvDict);
                    var v2 = GetVertex(t.V2, pntDict, normDict, uvDict);
                    var v3 = GetVertex(t.V3, pntDict, normDict, uvDict);
                    var fv = new FaceTriangle(v1, v2, v3);
                    ff.Triangles.Add(fv);
                }
                gg.Faces.Add(ff);
            }

            return gg;
        }

        class GeomBox
        {
            public Int32 Index { get; set; } = -1;

            public SVec3 Center { get; set; }

            public SortedSet<Int32> Pnts { get; set; }

            public SortedSet<Int32> Norms { get; set; }

            public SortedSet<Int32> Uvs { get; set; }
        }

        private GeomBox GetBoxIndex(Geometry g, IList<BoundingBox> boxes)
        {
            var gCenter = GetCenter(g);
            for (var i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].IsIn(gCenter.Center))
                {
                    gCenter.Index = i;
                    return gCenter;
                }
            }
            return gCenter;
        }

        private GeomBox GetCenter(Geometry g)
        {
            var ps = new SortedSet<Int32>();
            var ns = new SortedSet<Int32>();
            var ts = new SortedSet<Int32>();
            var sumX = 0.0f;
            var sumY = 0.0f;
            var sumZ = 0.0f;
            foreach (var f in g.Faces)
            {
                foreach (var t in f.Triangles)
                {
                    if (!ps.Contains(t.V1.V))
                    {
                        var v = Vertices[t.V1.V - 1];
                        sumX += v.X;
                        sumY += v.Y;
                        sumZ += v.Z;
                        ps.Add(t.V1.V);
                    }
                    if (!ps.Contains(t.V2.V))
                    {
                        var v = Vertices[t.V2.V - 1];
                        sumX += v.X;
                        sumY += v.Y;
                        sumZ += v.Z;
                        ps.Add(t.V2.V);
                    }
                    if (!ps.Contains(t.V3.V))
                    {
                        var v = Vertices[t.V3.V - 1];
                        sumX += v.X;
                        sumY += v.Y;
                        sumZ += v.Z;
                        ps.Add(t.V3.V);
                    }


                    if (t.V1.N > 0 && !ns.Contains(t.V1.N))
                    {
                        ns.Add(t.V1.N);
                    }
                    if (t.V1.T > 0 && !ts.Contains(t.V1.T))
                    {
                        ts.Add(t.V1.T);
                    }
                    if (t.V2.N > 0 && !ns.Contains(t.V2.N))
                    {
                        ns.Add(t.V2.N);
                    }
                    if (t.V2.T > 0 && !ts.Contains(t.V2.T))
                    {
                        ts.Add(t.V2.T);
                    }
                    if (t.V3.N > 0 && !ns.Contains(t.V3.N))
                    {
                        ns.Add(t.V3.N);
                    }
                    if (t.V3.T > 0 && !ts.Contains(t.V3.T))
                    {
                        ts.Add(t.V3.T);
                    }

                }
            }

            var x = sumX / ps.Count;
            var y = sumY / ps.Count;
            var z = sumZ / ps.Count;
            return new GeomBox
            {
                Center = new SVec3(x, y, z),
                Pnts = ps,
                Norms = ns,
                Uvs = ts
            };
        }

        public BoundingBox GetBounding()
        {
            var box = new BoundingBox();
            foreach (var v in Vertices)
            {
                var x = v.X;
                if (box.X.Min > x)
                {
                    box.X.Min = x;
                }
                else if (box.X.Max < x)
                {
                    box.X.Max = x;
                }
                var y = v.Y;
                if (box.Y.Min > y)
                {
                    box.Y.Min = y;
                }
                else if (box.Y.Max < y)
                {
                    box.Y.Max = y;
                }
                var z = v.Z;
                if (box.Z.Min > z)
                {
                    box.Z.Min = z;
                }
                else if (box.Z.Max < z)
                {
                    box.Z.Max = z;
                }
            }
            return box;
        }

        public Geometry[] GetOrAddGeometries(params String[] names)
        {
            var results = new List<Geometry>();
            foreach (var name in names)
            {
                if (_Geometries.ContainsKey(name)) { results.Add(_Geometries[name]); }
                else
                {
                    var newGeom = new Geometry(name);
                    _Geometries.Add(name, newGeom);
                    results.Add(newGeom);
                }
            }
            return results.ToArray();
        }
    }
}
