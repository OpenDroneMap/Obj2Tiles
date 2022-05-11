using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SilentWave.Obj2Gltf.WaveFront
{
    public interface IMtlParser
    {
        IEnumerable<Material> Parse(Stream stream, System.String searchPath, Encoding encoding = null);
        Material[] Parse(System.String path, System.String searchPath = null, Encoding encoding = null);
        Task<Material[]> ParseAsync(Stream stream, System.String searchPath, Encoding encoding = null);
        Task<Material[]> ParseAsync(System.String path, System.String searchPath = null, Encoding encoding = null);
    }
}