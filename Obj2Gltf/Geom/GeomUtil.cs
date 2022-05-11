using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace SilentWave.Obj2Gltf.Geom
{
    internal static class GeomUtil
    {
        public static PlanarAxis ComputeProjectTo2DArguments(IList<SVec3> positions)
        {
            var box = OrientedBoundingBox.FromPoints(positions);

            var halfAxis = box.HalfAxis;
            var xAxis = halfAxis.GetColumn(0);
            var yAxis = halfAxis.GetColumn(1);
            var zAxis = halfAxis.GetColumn(2);
            var xMag = xAxis.GetLength();
            var yMag = yAxis.GetLength();
            var zMag = zAxis.GetLength();
            var min = new[] { xMag, yMag, zMag }.Min();

            // If all the points are on a line return undefined because we can't draw a polygon
            if ((xMag == 0 && (yMag == 0 || zMag == 0)) || (yMag == 0 && zMag == 0))
            {
                return null;
            }

            var planeAxis1 = new SVec3();
            var planeAxis2 = new SVec3();

            if (min == yMag || min == zMag)
            {
                planeAxis1 = xAxis;
            }
            if (min == xMag)
            {
                planeAxis1 = yAxis;
            }
            else if (min == zMag)
            {
                planeAxis2 = yAxis;
            }
            if (min == xMag || min == yMag)
            {
                planeAxis2 = zAxis;
            }

            return new PlanarAxis
            {
                Center = box.Center,
                Axis1 = planeAxis1,
                Axis2 = planeAxis2
            };
        }


        private static SVec2 Project2D(SVec3 p, SVec3 center, SVec3 axis1, SVec3 axis2)
        {
            var v = p.Substract(center);
            var x = SVec3.Dot(axis1, v);
            var y = SVec3.Dot(axis2, v);

            return new SVec2(x, y);
        }

        public static IList<SVec2> CreateProjectPointsTo2DFunction(PlanarAxis axis, IList<SVec3> positions)
        {
            var pnts = new SVec2[positions.Count];
            for(var i = 0;i< pnts.Length;i++)
            {
                pnts[i] = Project2D(positions[i], axis.Center, axis.Axis1, axis.Axis2);
            }
            return pnts;
        }
    }
}
