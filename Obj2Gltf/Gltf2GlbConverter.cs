using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SilentWave.Obj2Gltf.Gltf;

namespace SilentWave.Obj2Gltf
{
    public class Gltf2GlbConverter
    {
        private const byte SpaceCharASCII = 0x20;

        public static Gltf2GlbConverter Factory() => new Gltf2GlbConverter();
        static string GetMimeTypeFromFileName(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToUpper();
            switch (ext)
            {
                case ".PNG":
                    return "image/png";
                case ".JPEG":
                case ".JPG":
                    return "image/jpeg";
                case ".WEBP":
                    return "image/webp";
                case ".KTX2":
                    return "image/ktx2";
                case ".GIF":
                    return "image/gif";
                default:
                    throw new ArgumentException("extension not supported");
            }
        }
        public void Convert(Gltf2GlbOptions options)
        {
            if (!File.Exists(options.InputPath)) throw new ArgumentException($"{options.InputPath} was not found");
            var currentDir = Path.GetDirectoryName(options.InputPath);
            var isCurrentInputFileATempFile = false;
            var currentInputFile = options.InputPath;

            var files2embed = new List<FileInfo>();
            var concatBufferLegth = 0L;
            var alteredJsonFile = Path.GetTempFileName();
            using (var reader = File.OpenText(currentInputFile))
            using (var writer = File.CreateText(alteredJsonFile))
            using (var jsonReader = new JsonTextReader(reader))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                var serializer = new JsonSerializer() { DefaultValueHandling = DefaultValueHandling.Ignore };
                jsonWriter.Formatting = options.MinifyJson ? Formatting.None : Formatting.Indented;
                var o = (JObject)JToken.ReadFrom(jsonReader);

                var buffersToken = o.SelectToken("buffers");
                var buffers = buffersToken.ToObject<Gltf.Buffer[]>();

                // Compute a 4-byte-aligned start offset for every source buffer inside the merged GLB
                // binary chunk. glTF requires that accessor.byteOffset + bufferView.byteOffset be a
                // multiple of the accessor's component size (up to 4 bytes for FLOAT/UNSIGNED_INT).
                // Because the per-attribute buffers are concatenated back-to-back, any buffer whose length
                // is not a multiple of 4 (e.g. an odd number of UNSIGNED_SHORT indices) would push every
                // following bufferView to a misaligned offset. Padding each buffer start to a 4-byte
                // boundary keeps all downstream accessors aligned.
                var bufferStartOffsets = new long[buffers.Length];
                for (var i = 0; i < buffers.Length; i++)
                {
                    var buffer = buffers[i];
                    if (buffer.Uri.StartsWith("data:")) throw new NotImplementedException("data uri are not supported yet");
                    concatBufferLegth = AlignToBoundary(concatBufferLegth);
                    bufferStartOffsets[i] = concatBufferLegth;
                    concatBufferLegth += buffer.ByteLength;
                    files2embed.Add(new FileInfo(Path.Combine(currentDir, buffer.Uri)));
                }

                var bufferViewsToken = o.SelectToken("bufferViews");
                var bufferViews = bufferViewsToken.ToObject<List<Gltf.BufferView>>();
                foreach (var bufferview in bufferViews)
                {
                    bufferview.ByteOffset += bufferStartOffsets[bufferview.Buffer.Value];
                    bufferview.Buffer = 0;
                }

                var imagesToken = o.SelectToken("images");
                if (imagesToken != null)
                {
                    var images = imagesToken.ToObject<Gltf.Image[]>();
                    foreach (var image in images)
                    {
                        if (image.Uri.StartsWith("data:")) throw new NotImplementedException("data uri are not supported yet");
                        var fileInfo = new FileInfo(Path.Combine(currentDir, image.Uri));
                        var bufferViewIndex = bufferViews.Count;
                        // Image bufferViews carry no accessor, but keeping every segment 4-byte aligned
                        // matches the physical padding written below and keeps the layout spec-clean.
                        concatBufferLegth = AlignToBoundary(concatBufferLegth);
                        bufferViews.Add(new BufferView() { Buffer = 0, ByteOffset = concatBufferLegth, ByteLength = fileInfo.Length, Name = fileInfo.Name });
                        image.Uri = null;
                        image.BufferView = bufferViewIndex;
                        image.MimeType = GetMimeTypeFromFileName(fileInfo.Name);
                        concatBufferLegth += fileInfo.Length;
                        files2embed.Add(fileInfo);
                    }
                    imagesToken.Replace(JToken.FromObject(images, serializer));
                }

                bufferViewsToken.Replace(JToken.FromObject(bufferViews, serializer));
                buffersToken.Replace(JToken.FromObject(new Gltf.Buffer[] { new Gltf.Buffer() { ByteLength = concatBufferLegth } }, serializer));

                o.WriteTo(jsonWriter);
            }

            currentInputFile = alteredJsonFile;
            isCurrentInputFileATempFile = true;

            using (var glbStream = File.Create(options.OutPutPath))
            using (var bw = new BinaryWriter(glbStream))
            using (var jsonFile = File.OpenRead(alteredJsonFile))
            {
                var asd = BitConverter.IsLittleEndian;
                // 0x4E4F534Au = JSON in ASCII
                // 0x004E4942u = BIN in ASCII
                // GLB Structure: (Global header) + (JSON chunk header) + (JSON chunk) + (Binary chunk header) + (Binary chunk)


                // Write binary glTF header (magic, version, length)
                bw.Write(0x46546C67u); //translates to glTF in ASCII
                bw.Write(2u);
                bw.Write(0u); // reserve space for total length
                // Write JSON Chunk header (length, type)
                bw.Write(0u); // reserve space for JsonLength
                bw.Write(0x4E4F534Au);
                bw.Flush();
                // Write JSON chunk
                jsonFile.CopyTo(glbStream);
                glbStream.Flush();
                // Write Trailing padding
                var paddingCount = GetTrailingCount(glbStream.Position);
                for (var byteWritten = 0; byteWritten < paddingCount; byteWritten++)
                {
                    bw.Write(SpaceCharASCII);
                }
                bw.Flush();
                glbStream.Seek(12, SeekOrigin.Begin);
                bw.Write((uint)(jsonFile.Length + paddingCount));
                glbStream.Seek(0, SeekOrigin.End);
                // Write Binary Chunk header (length, type)

                paddingCount = GetTrailingCount(concatBufferLegth);
                bw.Write((uint)(concatBufferLegth + paddingCount));
                bw.Write(0x004E4942u);
                bw.Flush();
                // Write Binary chunk. Insert 4-byte alignment padding between embedded segments so the
                // physical byte layout matches the bufferView byteOffsets computed above. Without this
                // padding, buffers whose length is not a multiple of 4 (such as an odd count of
                // UNSIGNED_SHORT indices) would misalign every following accessor and fail glTF/3D Tiles
                // validation.
                var binBytesWritten = 0L;
                foreach (var file in files2embed)
                {
                    var alignedStart = AlignToBoundary(binBytesWritten);
                    for (; binBytesWritten < alignedStart; binBytesWritten++)
                    {
                        bw.Write(byte.MinValue);
                    }
                    bw.Flush();
                    using (var stream = file.OpenRead())
                    {
                        stream.CopyTo(glbStream);
                    }
                    glbStream.Flush();
                    binBytesWritten += file.Length;
                }
                glbStream.Flush();
                // Write Trailing padding
                paddingCount = GetTrailingCount(glbStream.Position);
                for (var byteWritten = 0; byteWritten < paddingCount; byteWritten++)
                {
                    bw.Write(byte.MinValue);
                }
                glbStream.Flush();
                bw.Seek(8, SeekOrigin.Begin);
                bw.Write((uint)glbStream.Length);
            }

            if (isCurrentInputFileATempFile) File.Delete(currentInputFile);
            if (options.DeleteOriginal)
            {
                File.Delete(options.InputPath);
                foreach (var fileinfo in files2embed)
                    fileinfo.Delete();
            }
        }

        private byte GetTrailingCount(long length, byte boundary = 4)
        {
            var remainder = (byte)length % boundary;
            return remainder == byte.MinValue ? byte.MinValue : (byte)(boundary - remainder);
        }

        // Rounds a byte length up to the next multiple of the GLB/glTF alignment boundary (4 bytes).
        // Vertex-attribute accessors reference FLOAT (4-byte) components, so every bufferView they use
        // must start on a 4-byte boundary; padding source buffers to this boundary keeps the merged
        // binary chunk spec-compliant.
        private static long AlignToBoundary(long length, long boundary = 4)
        {
            var remainder = length % boundary;
            return remainder == 0 ? length : length + (boundary - remainder);
        }

    }
}
