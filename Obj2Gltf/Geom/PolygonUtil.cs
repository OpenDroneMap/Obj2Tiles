using System;
using System.Collections.Generic;
using System.Text;

namespace SilentWave.Obj2Gltf.Geom
{
    public class PolygonUtil
    {
        // 
        private static Boolean IsIntersect(SVec2 ln1Start, SVec2 ln1End, SVec2 ln2Start, SVec2 ln2End)
        {
            //https://ideone.com/PnPJgb
            var A = ln1Start;
            var B = ln1End;
            var C = ln2Start;
            var D = ln2End;
            var CmP = new SVec2(C.U - A.U, C.V - A.V);
            var r = new SVec2(B.U - A.U, B.V - A.V);
            var s = new SVec2(D.U - C.U, D.V - C.V);

            var CmPxr = CmP.U * r.V - CmP.V * r.U;
            var CmPxs = CmP.U * s.V - CmP.V * s.U;
            var rxs = r.U * s.V - r.V * s.U;

            if (CmPxr == 0f)
            {
                // Lines are collinear, and so intersect if they have any overlap

                return ((C.U - A.U < 0f) != (C.U - B.U < 0f))
                    || ((C.V - A.V < 0f) != (C.V - B.V < 0f));
            }

            if (rxs == 0f)
                return false; // Lines are parallel.

            var rxsr = 1f / rxs;
            var t = CmPxs * rxsr;
            var u = CmPxr * rxsr;

            return (t >= 0) && (t <= 1) && (u >= 0) && (u <= 1);



            //// https://en.wikipedia.org/wiki/Line%E2%80%93line_intersection
            //double x1 = ln1Start.U, y1 = ln1Start.V;
            //double x2 = ln1End.U, y2 = ln1End.V;
            //double x3 = ln2Start.U, y3 = ln2Start.V;
            //double x4 = ln2End.U, y4 = ln2End.V;

            //var t1 = (x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4);
            //var t2 = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            //var t = t1 / t2;

            //if (t < 0.0 || t > 1.0) return false;

            ////if (t >= 0.0 && t <= 1.0) return true;

            ////return false;

            //var u1 = (x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3);
            //var u2 = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            //var u = u1 / u2;

            //return u >= 0.0 && u <= 1.0;
        }

        public static PolygonPointRes CrossTest(SVec2 p, IList<SVec2> polygon, Single tol)
        {

            Single minX = Single.MaxValue, minY = Single.MaxValue, maxX = Single.MinValue, maxY = Single.MinValue;
            var angleVs = new List<Single>();
            var vecX = new SVec2(1.0f, 0);
            foreach (var v in polygon)
            {
                var d = p.GetDistance(v);
                if (d < tol) return PolygonPointRes.Vertex;
                if (minX > v.U) minX = v.U;
                if (minY > v.V) minY = v.V;
                if (maxX < v.U) maxX = v.U;
                if (maxY < v.V) maxY = v.V;
                var vector = new SVec2(v.U - p.U, v.V - p.V).Normalize();
                var an = (Single)Math.Acos(vecX.Dot(vector));
                if (vector.V < 0)
                {
                    an = 2 * (Single)Math.PI - an;
                }
                angleVs.Add(an);
            }
            for (var i = 0; i < polygon.Count; i++)
            {
                var j = i + 1;
                if (j == polygon.Count)
                {
                    j = 0;
                }
                var v1 = polygon[i];
                var v2 = polygon[j];
                var d0 = v1.GetDistance(v2);
                var distance = Math.Abs(p.GetDistance(v1) + p.GetDistance(v2) - d0);
                if (distance < tol)
                {
                    return PolygonPointRes.Edge;
                }
            }
            var box = new BoundingBox2 { Min = new SVec2(minX, minY), Max = new SVec2(maxX, maxY) };
            if (!box.IsIn(p)) return PolygonPointRes.Outside;

            angleVs.Sort();

            var startIndex = 0;
            var diff = angleVs[1] - angleVs[0];
            for (var i = 1; i < angleVs.Count; i++)
            {
                var j = i + 1;
                if (j == angleVs.Count) j = 0;
                var anJ = angleVs[j];
                if (j == 0)
                {
                }
                var diff1 = angleVs[j] - angleVs[i];
                if (diff1 > diff)
                {
                    diff = diff1;
                    startIndex = i;
                }
            }
            var angle = angleVs[startIndex] + diff / 2.0;
            var len = box.Max.GetDistance(box.Min);
            var p2 = new SVec2(len * (Single)Math.Cos(angle), len * (Single)Math.Sin(angle));

            var intersectCount = 0;
            for (var i = 0; i < polygon.Count; i++)
            {
                var j = i + 1;
                if (j == polygon.Count) j = 0;
                var v1 = polygon[i];
                var v2 = polygon[j];
                var pnt = IsIntersect(p, p2, v1, v2);
                IsIntersect(p, p2, v1, v2);
                if (pnt)
                {

                    intersectCount++;
                }
            }

            if (intersectCount % 2 == 1)
            {
                return PolygonPointRes.Inside;
            }
            return PolygonPointRes.Outside;
        }

        private static Single GetRayLength(SVec2 p, IList<SVec2> polygon)
        {
            throw new NotImplementedException();
        }
    }
}
