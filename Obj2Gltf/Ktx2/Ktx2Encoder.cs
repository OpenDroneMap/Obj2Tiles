using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SilentWave.Obj2Gltf.Ktx2
{
    /// <summary>
    /// Encodes a raster image (PNG/JPEG/...) into a KTX2 texture with Basis Universal supercompression
    /// by calling the KTX-Software native library (libktx) in-process via P/Invoke - no external
    /// executable is spawned. The library core does not resample mipmaps, so the full mip chain is
    /// generated here with ImageSharp and handed to libktx before compression.
    /// </summary>
    internal static class Ktx2Encoder
    {
        // KHR VkFormat enum values for the uncompressed source the encoder reads before supercompression.
        private const uint VK_FORMAT_R8G8B8_UNORM = 23;
        private const uint VK_FORMAT_R8G8B8_SRGB = 29;
        private const uint VK_FORMAT_R8G8B8A8_UNORM = 37;
        private const uint VK_FORMAT_R8G8B8A8_SRGB = 43;

        private const int KTX_TEXTURE_CREATE_ALLOC_STORAGE = 1;
        private const int KTX_SUCCESS = 0;

        // Bound the number of images encoded concurrently. libktx's Basis encoder is thread-safe for
        // concurrent per-texture calls once its one-time (non-thread-safe) global init has run - which
        // WarmUp forces on this serialized path - so we run one single-threaded encode per core
        // instead of serializing them.
        private static readonly SemaphoreSlim Gate =
            new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount));

        private static readonly object InitLock = new object();
        private static bool _initialized;
        private static string _libHint;

        [StructLayout(LayoutKind.Sequential)]
        private struct KtxTextureCreateInfo
        {
            public uint glInternalformat;
            public uint vkFormat;
            public IntPtr pDfd;
            public uint baseWidth;
            public uint baseHeight;
            public uint baseDepth;
            public uint numDimensions;
            public uint numLevels;
            public uint numLayers;
            public uint numFaces;
            public byte isArray;         // ktx_bool_t is 1 byte; DO NOT use C# bool (marshals as 4-byte BOOL)
            public byte generateMipmaps;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KtxBasisParams
        {
            public uint structSize;
            public byte uastc;
            public byte verbose;
            public byte noSSE;
            public uint threadCount;
            // ETC1S params
            public uint compressionLevel;
            public uint qualityLevel;
            public uint maxEndpoints;
            public float endpointRDOThreshold;
            public uint maxSelectors;
            public float selectorRDOThreshold;
            public byte inputSwizzle0;
            public byte inputSwizzle1;
            public byte inputSwizzle2;
            public byte inputSwizzle3;
            public byte normalMap;
            public byte separateRGToRGB_A;
            public byte preSwizzle;
            public byte noEndpointRDO;
            public byte noSelectorRDO;
            // UASTC params
            public uint uastcFlags;
            public byte uastcRDO;
            public float uastcRDOQualityScalar;
            public uint uastcRDODictSize;
            public float uastcRDOMaxSmoothBlockErrorScale;
            public float uastcRDOMaxSmoothBlockStdDev;
            public byte uastcRDODontFavorSimplerModes;
            public byte uastcRDONoMultithreading;
        }

        [DllImport("ktx", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ktxTexture2_Create(ref KtxTextureCreateInfo createInfo, int storageAllocation, out IntPtr newTex);

        [DllImport("ktx", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ktxTexture_GetData(IntPtr texture);

        [DllImport("ktx", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ktxTexture2_GetImageOffset(IntPtr texture, uint level, uint layer, uint faceSlice, out UIntPtr pOffset);

        [DllImport("ktx", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ktxTexture2_CompressBasisEx(IntPtr texture, ref KtxBasisParams parameters);

        [DllImport("ktx", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ktxTexture2_WriteToNamedFile(IntPtr texture, [MarshalAs(UnmanagedType.LPStr)] string dstname);

        [DllImport("ktx", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ktxTexture2_Destroy(IntPtr texture);

        [DllImport("ktx", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ktxErrorString(int error);

        /// <summary>
        /// Registers the native-library resolver (once) and verifies libktx can be loaded and called.
        /// Idempotent and thread-safe.
        /// </summary>
        public static void Initialize(GltfConverterOptions options)
        {
            if (_initialized) return;
            lock (InitLock)
            {
                if (_initialized) return;
                _libHint = options?.KtxToolPath;
                NativeLibrary.SetDllImportResolver(typeof(Ktx2Encoder).Assembly, ResolveNativeLibrary);
                try
                {
                    // Force-load the native library with a trivial call.
                    _ = ktxErrorString(KTX_SUCCESS);
                }
                catch (DllNotFoundException ex)
                {
                    throw new InvalidOperationException(
                        "KTX2 encoding requested but the native KTX-Software library (libktx) could not be loaded. " +
                        "Provide it next to the executable (ktx.dll / libktx.so / libktx.dylib), on the system " +
                        "library path, or point --ktx-path / OBJ2TILES_KTX at it.", ex);
                }
                // Trigger libktx's one-time (non-thread-safe) Basis encoder initialization serially
                // here so subsequent per-texture encodes can run concurrently without a lock.
                WarmUp(options);
                _initialized = true;
            }
        }

        // Resolves the "ktx" native library from the configured hint (file or directory), the
        // OBJ2TILES_KTX environment variable, or next to the running assembly, before falling back
        // to the OS default probing.
        private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (!string.Equals(libraryName, "ktx", StringComparison.Ordinal))
                return IntPtr.Zero;

            foreach (var candidate in EnumerateCandidates())
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate) &&
                    NativeLibrary.TryLoad(candidate, out var handle))
                {
                    return handle;
                }
            }
            return IntPtr.Zero; // let the runtime try its default search
        }

        private static System.Collections.Generic.IEnumerable<string> EnumerateCandidates()
        {
            var nativeName = OperatingSystem.IsWindows() ? "ktx.dll"
                : OperatingSystem.IsMacOS() ? "libktx.dylib" : "libktx.so";

            foreach (var hint in new[] { _libHint, Environment.GetEnvironmentVariable("OBJ2TILES_KTX") })
            {
                if (string.IsNullOrWhiteSpace(hint)) continue;
                if (File.Exists(hint))
                {
                    // Directly a native library, or a sibling of one (e.g. ktx.exe -> ktx.dll).
                    if (hint.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                        hint.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
                        hint.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase))
                        yield return hint;
                    var dir = Path.GetDirectoryName(hint);
                    if (!string.IsNullOrEmpty(dir)) yield return Path.Combine(dir, nativeName);
                }
                else if (Directory.Exists(hint))
                {
                    yield return Path.Combine(hint, nativeName);
                    yield return Path.Combine(hint, "bin", nativeName);
                }
            }

            var baseDir = AppContext.BaseDirectory;
            yield return Path.Combine(baseDir, nativeName);
            yield return Path.Combine(baseDir, "ktx", "bin", nativeName);
        }

        /// <summary>
        /// Encodes <paramref name="inputImagePath"/> (PNG/JPEG/...) into <paramref name="outputKtx2Path"/>.
        /// </summary>
        /// <param name="inputImagePath">Source raster image path.</param>
        /// <param name="outputKtx2Path">Destination .ktx2 path.</param>
        /// <param name="srgb">True for colour (sRGB) maps, false for linear data maps.</param>
        /// <param name="options">Encoder options.</param>
        public static void Encode(string inputImagePath, string outputKtx2Path, bool srgb, GltfConverterOptions options)
        {
            Gate.Wait();
            try
            {
                using var image = Image.Load<Rgba32>(inputImagePath);
                var width = image.Width;
                var height = image.Height;
                var hasAlpha = HasAlpha(image);
                var components = hasAlpha ? 4 : 3;
                var vkFormat = hasAlpha
                    ? (srgb ? VK_FORMAT_R8G8B8A8_SRGB : VK_FORMAT_R8G8B8A8_UNORM)
                    : (srgb ? VK_FORMAT_R8G8B8_SRGB : VK_FORMAT_R8G8B8_UNORM);
                var levels = 1 + (int)Math.Floor(Math.Log2(Math.Max(width, height)));

                var createInfo = new KtxTextureCreateInfo
                {
                    vkFormat = vkFormat,
                    baseWidth = (uint)width,
                    baseHeight = (uint)height,
                    baseDepth = 1,
                    numDimensions = 2,
                    numLevels = (uint)levels,
                    numLayers = 1,
                    numFaces = 1,
                    isArray = 0,
                    generateMipmaps = 0
                };

                var err = ktxTexture2_Create(ref createInfo, KTX_TEXTURE_CREATE_ALLOC_STORAGE, out var texture);
                Check(err, "ktxTexture2_Create");
                try
                {
                    var pData = ktxTexture_GetData(texture);
                    if (pData == IntPtr.Zero)
                        throw new InvalidOperationException("libktx returned a null image data pointer.");

                    for (var level = 0; level < levels; level++)
                    {
                        var lw = Math.Max(1, width >> level);
                        var lh = Math.Max(1, height >> level);
                        var buffer = ExtractLevelPixels(image, lw, lh, components);

                        err = ktxTexture2_GetImageOffset(texture, (uint)level, 0, 0, out var offset);
                        Check(err, "ktxTexture2_GetImageOffset");

                        var dest = new IntPtr(pData.ToInt64() + (long)offset.ToUInt64());
                        Marshal.Copy(buffer, 0, dest, buffer.Length);
                    }

                    var basisParams = BuildParams(options);
                    err = ktxTexture2_CompressBasisEx(texture, ref basisParams);
                    Check(err, "ktxTexture2_CompressBasisEx");

                    err = ktxTexture2_WriteToNamedFile(texture, outputKtx2Path);
                    Check(err, "ktxTexture2_WriteToNamedFile");
                }
                finally
                {
                    ktxTexture2_Destroy(texture);
                }
            }
            finally
            {
                Gate.Release();
            }
        }

        // Encode one tiny texture to force libktx's lazy, non-thread-safe Basis encoder init to run
        // once on this (serialized) code path before any concurrent encoding begins.
        private static void WarmUp(GltfConverterOptions options)
        {
            var createInfo = new KtxTextureCreateInfo
            {
                vkFormat = VK_FORMAT_R8G8B8_SRGB,
                baseWidth = 4,
                baseHeight = 4,
                baseDepth = 1,
                numDimensions = 2,
                numLevels = 1,
                numLayers = 1,
                numFaces = 1
            };
            if (ktxTexture2_Create(ref createInfo, KTX_TEXTURE_CREATE_ALLOC_STORAGE, out var texture) != KTX_SUCCESS)
                return;
            try
            {
                var pData = ktxTexture_GetData(texture);
                if (pData != IntPtr.Zero)
                {
                    var pixels = new byte[4 * 4 * 3];
                    Marshal.Copy(pixels, 0, pData, pixels.Length);
                    var warmParams = BuildParams(options);
                    ktxTexture2_CompressBasisEx(texture, ref warmParams);
                }
            }
            catch
            {
                // A warm-up failure is non-fatal; the first real encode will surface any real error.
            }
            finally
            {
                ktxTexture2_Destroy(texture);
            }
        }

        private static KtxBasisParams BuildParams(GltfConverterOptions options)
        {
            var uastc = options.Ktx2Uastc;
            // Each texture is encoded single-threaded; parallelism comes from encoding many textures
            // (tiles) concurrently. Callers can override with an explicit thread count.
            var threads = options.Ktx2Threads > 0 ? (uint)options.Ktx2Threads : 1u;
            var p = new KtxBasisParams
            {
                structSize = (uint)Marshal.SizeOf<KtxBasisParams>(),
                uastc = (byte)(uastc ? 1 : 0),
                threadCount = threads
            };
            if (uastc)
            {
                // Least-significant 4 bits select the UASTC speed/quality level [0,4].
                p.uastcFlags = (uint)Math.Clamp(options.Ktx2QualityLevel, 0, 4);
            }
            else
            {
                p.compressionLevel = (uint)Math.Clamp(options.Ktx2CompressionLevel, 0, 6);
                p.qualityLevel = (uint)Math.Clamp(options.Ktx2QualityLevel, 1, 255);
            }
            return p;
        }

        // Resizes the base image to the requested mip dimensions (Lanczos) and packs tightly to the
        // requested component count (RGB or RGBA), matching KTX2's unpadded level layout.
        private static byte[] ExtractLevelPixels(Image<Rgba32> baseImage, int levelWidth, int levelHeight, int components)
        {
            using var level = baseImage.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(levelWidth, levelHeight),
                Sampler = KnownResamplers.Lanczos3,
                Mode = ResizeMode.Stretch
            }));

            var buffer = new byte[levelWidth * levelHeight * components];
            var index = 0;
            level.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                    {
                        var px = row[x];
                        buffer[index++] = px.R;
                        buffer[index++] = px.G;
                        buffer[index++] = px.B;
                        if (components == 4) buffer[index++] = px.A;
                    }
                }
            });
            return buffer;
        }

        private static bool HasAlpha(Image<Rgba32> image)
        {
            var found = false;
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height && !found; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                    {
                        if (row[x].A < 255) { found = true; break; }
                    }
                }
            });
            return found;
        }

        private static void Check(int err, string what)
        {
            if (err == KTX_SUCCESS) return;
            string message;
            try
            {
                var ptr = ktxErrorString(err);
                message = ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : null;
            }
            catch
            {
                message = null;
            }
            throw new InvalidOperationException($"{what} failed: {message ?? ("KTX error " + err)} (code {err}).");
        }
    }
}
