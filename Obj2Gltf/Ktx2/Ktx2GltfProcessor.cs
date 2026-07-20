using System;
using System.IO;
using SilentWave.Obj2Gltf.Gltf;

namespace SilentWave.Obj2Gltf.Ktx2
{
    /// <summary>
    /// Post-processes a built glTF model, re-encoding every referenced raster image to KTX2 (Basis
    /// Universal) and rewriting the textures to use the KHR_texture_basisu extension. Intended to run
    /// after the model is built but before the glTF is written / converted to GLB.
    /// </summary>
    internal static class Ktx2GltfProcessor
    {
        /// <summary>
        /// Applies KTX2 encoding to <paramref name="model"/> in place.
        /// </summary>
        /// <param name="model">The glTF model to process.</param>
        /// <param name="gltfDir">Directory the image URIs are relative to (the .gltf output folder).</param>
        /// <param name="options">Converter options carrying the KTX2 settings.</param>
        public static void Apply(GltfModel model, string gltfDir, GltfConverterOptions options)
        {
            if (model?.Images == null || model.Images.Count == 0) return;

            // Load and verify the native KTX-Software library (libktx) once.
            Ktx2Encoder.Initialize(options);

            // Every image index that was successfully encoded maps to its new .ktx2 filename.
            var encoded = new string[model.Images.Count];
            for (var i = 0; i < model.Images.Count; i++)
            {
                var image = model.Images[i];
                if (string.IsNullOrEmpty(image.Uri)) continue;
                if (image.Uri.EndsWith(".ktx2", StringComparison.OrdinalIgnoreCase))
                {
                    encoded[i] = image.Uri;
                    continue;
                }

                var inputFull = Path.Combine(gltfDir, image.Uri);
                if (!File.Exists(inputFull))
                    throw new FileNotFoundException($"Texture referenced by glTF not found: {inputFull}");

                var ktx2Name = Path.GetFileNameWithoutExtension(image.Uri) + ".ktx2";
                var outputFull = Path.Combine(gltfDir, ktx2Name);

                // In this pipeline all textures are baseColor (diffuse) maps, hence sRGB. The hook is
                // kept generic for future linear data maps (e.g. normals).
                Ktx2Encoder.Encode(inputFull, outputFull, srgb: true, options);

                image.Uri = ktx2Name;
                image.MimeType = "image/ktx2";
                encoded[i] = ktx2Name;

                // The original raster is now superseded by the KTX2; remove it so it is not left behind.
                try
                {
                    if (!string.Equals(inputFull, outputFull, StringComparison.OrdinalIgnoreCase))
                        File.Delete(inputFull);
                }
                catch { /* ignore */ }
            }

            // Rewrite each texture to reference its image through KHR_texture_basisu, dropping the base
            // source (and any EXT_texture_webp) so a KTX2-aware loader selects the Basis image.
            var used = false;
            foreach (var t in model.Textures)
            {
                var srcIndex = t.Source;
                if (srcIndex == null && t.Extensions?.EXT_texture_webp != null)
                    srcIndex = t.Extensions.EXT_texture_webp.Source;
                if (srcIndex == null || srcIndex.Value < 0 || srcIndex.Value >= encoded.Length) continue;
                if (encoded[srcIndex.Value] == null) continue;

                t.Extensions ??= new TextureExtensions();
                t.Extensions.KHR_texture_basisu = new KhrTextureBasisu { Source = srcIndex.Value };
                t.Extensions.EXT_texture_webp = null;
                t.Source = null;
                used = true;
            }

            if (used)
                model.UseExtension("KHR_texture_basisu", required: true);
        }
    }
}
