using System.Text;

namespace Obj2Tiles.Tiles
{
    public class B3dm
    {
        public B3dm()
        {
            B3dmHeader = new B3dmHeader();
            FeatureTableJson = string.Empty;
            BatchTableJson = string.Empty;
            FeatureTableJson = "{\"BATCH_LENGTH\":0}  ";
            FeatureTableBinary = Array.Empty<byte>();
            BatchTableBinary = Array.Empty<byte>();
        }

        public B3dm(byte[] glb): this()
        {
            GlbData = glb;
        }

        public B3dmHeader B3dmHeader { get; set; }
        public string FeatureTableJson { get; set; }
        public byte[] FeatureTableBinary { get; set; }
        public string BatchTableJson { get; set; }
        public byte[] BatchTableBinary { get; set; }
        public byte[] GlbData { get; set; }

        public byte[] ToBytes()
        {
            const int headerLength = 28;

            var featureTableJson = BufferPadding.AddPadding(FeatureTableJson, headerLength);
            var batchTableJson = BufferPadding.AddPadding(BatchTableJson);
            var featureTableBinary = BufferPadding.AddPadding(FeatureTableBinary);
            var batchTableBinary = BufferPadding.AddPadding(BatchTableBinary);

            // Ensure GLB data is padded to 8-byte alignment
            GlbData = BufferPadding.AddPadding(GlbData);

            B3dmHeader.ByteLength = GlbData.Length + headerLength + featureTableJson.Length + Encoding.UTF8.GetByteCount(batchTableJson) + batchTableBinary.Length + FeatureTableBinary.Length;

            B3dmHeader.FeatureTableJsonByteLength = featureTableJson.Length;
            B3dmHeader.BatchTableJsonByteLength = Encoding.UTF8.GetByteCount(batchTableJson);
            B3dmHeader.FeatureTableBinaryByteLength =featureTableBinary.Length;
            B3dmHeader.BatchTableBinaryByteLength = batchTableBinary.Length;

            var memoryStream = new MemoryStream();
            var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(B3dmHeader.AsBinary());
            binaryWriter.Write(Encoding.UTF8.GetBytes(featureTableJson));
            if (featureTableBinary != null) {
                binaryWriter.Write(featureTableBinary);
            }
            binaryWriter.Write(Encoding.UTF8.GetBytes(batchTableJson));
            if (batchTableBinary != null) {
                binaryWriter.Write(batchTableBinary);
            }
            binaryWriter.Write(GlbData);
            binaryWriter.Flush();
            binaryWriter.Close();
            return memoryStream.ToArray();
        }
    }
}
