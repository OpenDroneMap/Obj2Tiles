#region License
/*
MIT License

Copyright(c) 2017-2018 Mattias Edlund

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
#endregion

namespace MeshDecimatorCore.Algorithms
{
    /// <summary>
    /// A decimation algorithm.
    /// </summary>
    public abstract class DecimationAlgorithm
    {
        #region Delegates
        /// <summary>
        /// A callback for decimation status reports.
        /// </summary>
        /// <param name="iteration">The current iteration, starting at zero.</param>
        /// <param name="originalTris">The original count of triangles.</param>
        /// <param name="currentTris">The current count of triangles.</param>
        /// <param name="targetTris">The target count of triangles.</param>
        public delegate void StatusReportCallback(int iteration, int originalTris, int currentTris, int targetTris);
        #endregion

        #region Fields

        private int maxVertexCount = 0;

        private StatusReportCallback statusReportInvoker = null;
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets if borders should be preserved.
        /// Default value: false
        /// </summary>
        public bool PreserveBorders { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum vertex count. Set to zero for no limitation.
        /// Default value: 0 (no limitation)
        /// </summary>
        public int MaxVertexCount
        {
            get => maxVertexCount;
            set => maxVertexCount = Math.MathHelper.Max(value, 0);
        }

        /// <summary>
        /// Gets or sets if verbose information should be printed in the console.
        /// Default value: false
        /// </summary>
        public bool Verbose { get; set; } = false;

        #endregion

        #region Events
        /// <summary>
        /// An event for status reports for this algorithm.
        /// </summary>
        public event StatusReportCallback StatusReport
        {
            add { statusReportInvoker += value; }
            remove { statusReportInvoker -= value; }
        }
        #endregion

        #region Protected Methods
        /// <summary>
        /// Reports the current status of the decimation.
        /// </summary>
        /// <param name="iteration">The current iteration, starting at zero.</param>
        /// <param name="originalTris">The original count of triangles.</param>
        /// <param name="currentTris">The current count of triangles.</param>
        /// <param name="targetTris">The target count of triangles.</param>
        protected void ReportStatus(int iteration, int originalTris, int currentTris, int targetTris)
        {
            var statusReportInvoker = this.statusReportInvoker;
            if (statusReportInvoker != null)
            {
                statusReportInvoker.Invoke(iteration, originalTris, currentTris, targetTris);
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initializes the algorithm with the original mesh.
        /// </summary>
        /// <param name="mesh">The mesh.</param>
        public abstract void Initialize(Mesh mesh);

        /// <summary>
        /// Decimates the mesh.
        /// </summary>
        /// <param name="targetTrisCount">The target triangle count.</param>
        public abstract void DecimateMesh(int targetTrisCount);

        /// <summary>
        /// Decimates the mesh without losing any quality.
        /// </summary>
        public abstract void DecimateMeshLossless();

        /// <summary>
        /// Returns the resulting mesh.
        /// </summary>
        /// <returns>The resulting mesh.</returns>
        public abstract Mesh ToMesh();
        #endregion
    }
}