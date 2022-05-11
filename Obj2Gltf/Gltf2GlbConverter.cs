using SilentWave.Gltf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SilentWave
{
    public class Gltf2GlbConverter
    {
        private const Byte SpaceCharASCII = 0x20;

        public static Gltf2GlbConverter Factory() => new Gltf2GlbConverter();
        static String GetMimeTypeFromFileName(String fileName)
        {
            var ext = Path.GetExtension(fileName).ToUpper();
            switch (ext)
            {
                case ".PNG":
                    return "image/png";
                case ".JPEG":
                case ".JPG":
                    return "image/jpeg";
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
                foreach (var buffer in buffers)
                {
                    if (buffer.Uri.StartsWith("data:")) throw new NotImplementedException("data uri are not supported yet");
                    var fileInfo = new FileInfo(Path.Combine(currentDir, buffer.Uri));
                    files2embed.Add(fileInfo);
                }
                concatBufferLegth += buffers.Sum(x => x.ByteLength);

                var bufferViewsToken = o.SelectToken("bufferViews");
                var bufferViews = bufferViewsToken.ToObject<List<Gltf.BufferView>>();
                foreach (var bufferview in bufferViews)
                {
                    for (var bufferIndex = bufferview.Buffer.Value; bufferIndex > 0; bufferIndex--)
                    {
                        bufferview.ByteOffset += buffers[bufferIndex - 1].ByteLength;
                    }
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
                bw.Write((UInt32)(jsonFile.Length + paddingCount));
                glbStream.Seek(0, SeekOrigin.End);
                // Write Binary Chunk header (length, type)

                paddingCount = GetTrailingCount(concatBufferLegth);
                bw.Write((UInt32)(concatBufferLegth + paddingCount));
                bw.Write(0x004E4942u);
                bw.Flush();
                // Write Binary chunk
                foreach (var file in files2embed)
                {
                    using (var stream = file.OpenRead())
                    {
                        stream.CopyTo(glbStream);
                    }
                }
                glbStream.Flush();
                // Write Trailing padding
                paddingCount = GetTrailingCount(glbStream.Position);
                for (var byteWritten = 0; byteWritten < paddingCount; byteWritten++)
                {
                    bw.Write(Byte.MinValue);
                }
                glbStream.Flush();
                bw.Seek(8, SeekOrigin.Begin);
                bw.Write((UInt32)glbStream.Length);
            }

            if (isCurrentInputFileATempFile) File.Delete(currentInputFile);
            if (options.DeleteOriginal)
            {
                File.Delete(options.InputPath);
                foreach (var fileinfo in files2embed)
                    fileinfo.Delete();
            }
        }

        private Byte GetTrailingCount(Int64 length, Byte boundary = 4)
        {
            var remainder = (Byte)length % boundary;
            return remainder == Byte.MinValue ? Byte.MinValue : (Byte)(boundary - remainder);
        }

    }
}
