using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SilentWave.Obj2Gltf.WaveFront
{
    public interface IMtlParser
    {
        IEnumerable<Material> Parse(Stream stream, string searchPath, Encoding encoding = null);
        Material[] Parse(string path, string searchPath = null, Encoding encoding = null);
        Task<Material[]> ParseAsync(Stream stream, string searchPath, Encoding encoding = null);
        Task<Material[]> ParseAsync(string path, string searchPath = null, Encoding encoding = null);
    }
}