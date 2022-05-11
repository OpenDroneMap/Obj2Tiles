using System.Text;

namespace Obj2Tiles.Tiles
{
    public static class BufferPadding
    {
        private static int boundary = 8;
        public static byte[] AddPadding(byte[] bytes, int offset=0)
        {
            var remainder = (offset + bytes.Length) % boundary;
            var padding = (remainder == 0) ? 0 : boundary - remainder;
            var whitespace = new string(' ', padding);
            var paddingBytes = Encoding.UTF8.GetBytes(whitespace);
            var res = bytes.Concat(paddingBytes);
            return res.ToArray();
        }
        public static string AddPadding(string input, int offset=0)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var paddedBytes = BufferPadding.AddPadding(bytes, offset);
            var result = Encoding.UTF8.GetString(paddedBytes);
            return result;
        }
    }
}
