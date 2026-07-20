using System;
using System.Text;

namespace SilentWave.Obj2Gltf
{
    public class GltfConverterOptions
    {
        /// <summary>
        /// obj and mtl files' text encoding
        /// </summary>
        public Encoding ObjEncoding { get; set; }

        /// <summary>
        /// Default is false
        /// </summary>
        public bool RemoveDegenerateFaces { get; set; } = false;

        /// <summary>
        /// Default is false
        /// </summary>
        public bool DeleteOriginals { get; set; } = false;

        /// <summary>
        /// When true, every referenced raster texture is re-encoded to KTX2 (Basis Universal) and the
        /// textures are rewritten to use the KHR_texture_basisu extension. Requires the KTX-Software
        /// "ktx" command-line tool to be resolvable (see <see cref="KtxToolPath"/>).
        /// </summary>
        public bool EncodeKtx2 { get; set; } = false;

        /// <summary>
        /// When true, use UASTC (higher quality, larger) instead of the default ETC1S/BasisLZ mode.
        /// </summary>
        public bool Ktx2Uastc { get; set; } = false;

        /// <summary>
        /// ETC1S/BasisLZ quality level [1,255] (higher = better quality/larger). Default 128.
        /// When <see cref="Ktx2Uastc"/> is set this is reinterpreted as the UASTC quality level [0,4].
        /// </summary>
        public int Ktx2QualityLevel { get; set; } = 128;

        /// <summary>
        /// ETC1S/BasisLZ compression level [0,6] (speed vs. quality). Default 1.
        /// </summary>
        public int Ktx2CompressionLevel { get; set; } = 1;

        /// <summary>
        /// Zstandard supercompression level [1,22] applied to UASTC output only. Default 18.
        /// </summary>
        public int Ktx2ZstdLevel { get; set; } = 18;

        /// <summary>
        /// Number of encoder threads per ktx invocation. 0 lets ktx pick (hardware concurrency).
        /// </summary>
        public int Ktx2Threads { get; set; } = 0;

        /// <summary>
        /// Optional path to the native KTX-Software library (libktx: ktx.dll / libktx.so / libktx.dylib)
        /// or the directory containing it, used for KTX2 encoding via P/Invoke. When null the library is
        /// resolved from the OBJ2TILES_KTX environment variable, next to the executable, or on the
        /// system library path.
        /// </summary>
        public string KtxToolPath { get; set; } = null;
    }
}
