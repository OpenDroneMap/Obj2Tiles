using System.IO;
using Newtonsoft.Json;

namespace SilentWave.Obj2Gltf
{
    public static class JsonHelper
    {
        public static void Minify(StreamReader input, StreamWriter output) => ReformatJson(input, output, Formatting.None);

        public static void Beautify(StreamReader input, StreamWriter output) => ReformatJson(input, output, Formatting.Indented);

        public static void ReformatJson(TextReader stringReader, TextWriter stringWriter, Formatting formatting)
        {
            using (var jsonReader = new JsonTextReader(stringReader))
            using (var jsonWriter = new JsonTextWriter(stringWriter))
            {
                jsonWriter.Formatting = formatting;
                jsonWriter.WriteToken(jsonReader);
            }
        }
    }
}
