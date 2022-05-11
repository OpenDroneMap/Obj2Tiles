using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace SilentWave.Obj2Gltf.Geom
{
    // Cesium
    internal class Matrix3
    {
        private readonly Single[] _arr = new Single[9];

        public Matrix3(Single column0Row0, Single column1Row0, Single column2Row0,
                           Single column0Row1, Single column1Row1, Single column2Row1,
                           Single column0Row2, Single column1Row2, Single column2Row2)
        {
            _arr[0] = column0Row0;
            _arr[1] = column0Row1;
            _arr[2] = column0Row2;
            _arr[3] = column1Row0;
            _arr[4] = column1Row1;
            _arr[5] = column1Row2;
            _arr[6] = column2Row0;
            _arr[7] = column2Row1;
            _arr[8] = column2Row2;
        }

        public SVec3 GetColumn(Int32 index)
        {
            switch(index)
            {
                case 0:
                    return new SVec3(_arr[0], _arr[1], _arr[2]);
                case 1:
                    return new SVec3(_arr[3], _arr[4], _arr[5]);
                case 2:
                    return new SVec3(_arr[6], _arr[7], _arr[8]);
                default:
                    throw new Exception($"Invalid index {index}");
            }
        }

        public static Matrix3 Identity
        {
            get { return new Matrix3(1, 0, 0, 0, 1, 0, 0, 0, 1); }
        }

        public Matrix3 Clone()
        {
            return new Matrix3(_arr[0], _arr[1], _arr[2], _arr[3], _arr[4], _arr[5], _arr[6], _arr[7], _arr[8]);
        }

        private Single ComputeFrobeniusNorm()
        {
            return (Single)Math.Sqrt(_arr.Select(c => c * c).Sum());
        }

        private static Int32 GetArrayIndex(Int32 row, Int32 col)
        {
            return row + col * 3;
        }

        public Matrix3 Transpose()
        {
            var matrix = _arr;
            var column0Row0 = matrix[0];
            var column0Row1 = matrix[3];
            var column0Row2 = matrix[6];
            var column1Row0 = matrix[1];
            var column1Row1 = matrix[4];
            var column1Row2 = matrix[7];
            var column2Row0 = matrix[2];
            var column2Row1 = matrix[5];
            var column2Row2 = matrix[8];

            return new Matrix3(column0Row0, column0Row1, column0Row2, column1Row0, column1Row1, column1Row2, column2Row0, column2Row1, column2Row2);
        }

        public Single this[Int32 row, Int32 col]
        {
            get { return _arr[GetArrayIndex(row, col)]; }
            set
            {
                var index = GetArrayIndex(row, col);
                _arr[index] = value;
            }
        }

        private Single OffDiagonalFrobeniusNorm()
        {
            var a = _arr[GetArrayIndex(0, 0)];
            var b = _arr[GetArrayIndex(1, 1)];
            var c = _arr[GetArrayIndex(2, 2)];
            return (Single)Math.Sqrt(a * a + b * b + c * c);
        }

        static Int32[] rowVal =  { 1, 0, 0 };
        static Int32[] colVal = { 2, 2, 1 };
        /// <summary>
        /// This routine was created based upon Matrix Computations, 3rd ed., by Golub and Van Loan,
        // section 8.4.2 The 2by2 Symmetric Schur Decomposition.
        ///
        /// The routine takes a matrix, which is assumed to be symmetric, and
        /// finds the largest off-diagonal term, and then creates
        // a matrix (result) which can be used to help reduce it
        /// </summary>
        /// <returns></returns>
        private Matrix3 ShurDecomposition()
        {
            var tolerance = 1e-15;
            var maxDiagonal = 0.0;
            var rotAxis = 1;

            // find pivot (rotAxis) based on max diagonal of matrix
            for(var i = 0;i<3;++i)
            {
                var temp = Math.Abs(this[rowVal[i], colVal[i]]);
                if (temp > maxDiagonal)
                {
                    rotAxis = i;
                    maxDiagonal = temp;
                }
            }
            var c = 1.0f;
            var s = 0.0f;

            var p = rowVal[rotAxis];
            var q = colVal[rotAxis];

            if (Math.Abs(this[p, q]) > tolerance)
            {
                var qq = this[q, q];
                var pp = this[p, p];
                var qp = this[p, q];

                var tau = (qq - pp) / (2.0f * qp);
                Single t;
                if (tau < 0)
                {
                    t = -1.0f / (-tau + (Single)Math.Sqrt(1.0 + tau * tau));
                }
                else
                {
                    t = 1.0f / (tau + (Single)Math.Sqrt(1.0 + tau * tau));
                }

                c = 1.0f / (Single)Math.Sqrt(1.0f + t * t);
                s = t * c;
            }

            var result = Identity.Clone();
            result[p, p] = result[q, q] = c;
            result[p, q] = s;
            result[q, p] = -s;

            return result;
        }

        public Single this[Int32 index]
        {
            get { return _arr[index]; }
        }

        public Matrix3 MultiplyByScale(SVec3 scale)
        {
            var matrix = _arr;
            var result = new Single[9];

            result[0] = matrix[0] * scale.X;
            result[1] = matrix[1] * scale.X;
            result[2] = matrix[2] * scale.X;
            result[3] = matrix[3] * scale.Y;
            result[4] = matrix[4] * scale.Y;
            result[5] = matrix[5] * scale.Y;
            result[6] = matrix[6] * scale.Z;
            result[7] = matrix[7] * scale.Z;
            result[8] = matrix[8] * scale.Z;

            return new Matrix3(result[0], result[1], result[2], result[3], result[4], result[5], result[6], result[7], result[8]);
        }

        public static Matrix3 Multiply(Matrix3 left, Matrix3 right)
        {
            var column0Row0 = left[0] * right[0] + left[3] * right[1] + left[6] * right[2];
            var column0Row1 = left[1] * right[0] + left[4] * right[1] + left[7] * right[2];
            var column0Row2 = left[2] * right[0] + left[5] * right[1] + left[8] * right[2];

            var column1Row0 = left[0] * right[3] + left[3] * right[4] + left[6] * right[5];
            var column1Row1 = left[1] * right[3] + left[4] * right[4] + left[7] * right[5];
            var column1Row2 = left[2] * right[3] + left[5] * right[4] + left[8] * right[5];

            var column2Row0 = left[0] * right[6] + left[3] * right[7] + left[6] * right[8];
            var column2Row1 = left[1] * right[6] + left[4] * right[7] + left[7] * right[8];
            var column2Row2 = left[2] * right[6] + left[5] * right[7] + left[8] * right[8];

            return new Matrix3(column0Row0, column0Row1, column0Row2, column1Row0, column1Row1, column1Row2, column2Row0, column2Row1, column2Row2);
        }
        /// <summary>
        /// Computes the eigenvectors and eigenvalues of a symmetric matrix.
        /// </summary>
        /// <returns>{Item1: diagMatrix, Item2: unitaryMatrix}</returns>
        public Tuple<Matrix3, Matrix3> ComputeEigenDecomposition()
        {
            var tolerance = 1e-20;
            var maxSweeps = 10;

            var count = 0;
            var sweep = 0;

            var epsilon = tolerance * ComputeFrobeniusNorm();

            var diagMatrix = Clone();
            var unitaryMatrix = Identity.Clone();

            while (sweep < maxSweeps && diagMatrix.OffDiagonalFrobeniusNorm() > epsilon)
            {
                var jMatrix = diagMatrix.ShurDecomposition();
                var jMatrixTranspose = jMatrix.Transpose();
                diagMatrix = Multiply(diagMatrix, jMatrix);
                diagMatrix = Multiply(jMatrixTranspose, diagMatrix);
                unitaryMatrix = Multiply(unitaryMatrix, jMatrix);

                if (++count > 2)
                {
                    ++sweep;
                    count = 0;
                }
            }

            return new Tuple<Matrix3, Matrix3>(diagMatrix, unitaryMatrix);
        }
    }
}
