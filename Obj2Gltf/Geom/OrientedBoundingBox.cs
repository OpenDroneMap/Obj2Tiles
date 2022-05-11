using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace SilentWave.Obj2Gltf.Geom
{
    // from cesium
    internal class OrientedBoundingBox
    {
        public Matrix3 HalfAxis { get; set; }

        public SVec3 Center { get; set; }


        public static OrientedBoundingBox FromPoints(IList<SVec3> positions)
        {
            var result = new OrientedBoundingBox();

            var length = positions.Count;

            var meanPoint = positions[0];
            for (var i = 1; i < length; i++)
            {
                meanPoint = SVec3.Sum(meanPoint, positions[i]);
            }
            var invLength = 1.0f / length;

            meanPoint = meanPoint.MultiplyBy(invLength);

            var exx = 0.0f;
            var exy = 0.0f;
            var exz = 0.0f;
            var eyy = 0.0f;
            var eyz = 0.0f;
            var ezz = 0.0f;

            for (var i = 0; i < length; i++)
            {
                var p = positions[i].Substract(meanPoint);
                exx += p.X * p.X;
                exy += p.X * p.Y;
                exz += p.X * p.Z;
                eyy += p.Y * p.Y;
                eyz += p.Y * p.Z;
                ezz += p.Z * p.Z;
            }

            exx *= invLength;
            exy *= invLength;
            exz *= invLength;
            eyy *= invLength;
            eyz *= invLength;
            ezz *= invLength;

            var covarianceMatrix = new Matrix3(exx, exy, exz, exy, eyy, eyz, exz, eyz, ezz);

            var eigenDecomposition = covarianceMatrix.ComputeEigenDecomposition();
            var diagMatrix = eigenDecomposition.Item1;
            var unitaryMatrix = eigenDecomposition.Item2;
            var rotation = unitaryMatrix.Clone();

            var v1 = rotation.GetColumn(0);
            var v2 = rotation.GetColumn(1);
            var v3 = rotation.GetColumn(2);

            var u1 = Single.MinValue; //-Number.MAX_VALUE;
            var u2 = Single.MinValue; //-Number.MAX_VALUE;
            var u3 = Single.MinValue; //-Number.MAX_VALUE;
            var l1 = Single.MaxValue; //Number.MAX_VALUE;
            var l2 = Single.MaxValue; //Number.MAX_VALUE;
            var l3 = Single.MaxValue; //Number.MAX_VALUE;

            for (var i = 0; i < length; i++)
            {
                var p = positions[i];
                u1 = new[] { SVec3.Dot(v1, p), u1 }.Max();
                u2 = new[] { SVec3.Dot(v2, p), u2 }.Max();
                u3 = new[] { SVec3.Dot(v3, p), u3 }.Max();

                l1 = new[] { SVec3.Dot(v1, p), l1 }.Min();
                l2 = new[] { SVec3.Dot(v2, p), l2 }.Min();
                l3 = new[] { SVec3.Dot(v3, p), l3 }.Min();
            }

            v1 = v1.MultiplyBy(0.5f * (l1 + u1));
            v2 = v2.MultiplyBy(0.5f * (l2 + u2));
            v3 = v3.MultiplyBy(0.5f * (l3 + u3));

            var center = SVec3.Sum(v1, v2);
            center = SVec3.Sum(center, v3);

            var scale = new SVec3(u1 - l1, u2 - l2, u3 - l3);
            scale = scale.MultiplyBy(0.5f);

            rotation = rotation.MultiplyByScale(scale);

            return new OrientedBoundingBox
            {
                Center = center,
                HalfAxis = rotation
            };
        }
    }
}
