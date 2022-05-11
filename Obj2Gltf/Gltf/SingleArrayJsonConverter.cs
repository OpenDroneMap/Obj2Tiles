using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Newtonsoft.Json;

namespace SilentWave.Gltf
{
    /// <summary>
    /// Convert to int[] when all values are equals integers
    /// </summary>
    public class SingleArrayJsonConverter : JsonConverter<Single[]>
    {
        public override Single[] ReadJson(JsonReader reader,
                                          Type objectType,
                                          Single[] existingValue,
                                          Boolean hasExistingValue,
                                          JsonSerializer serializer)
        {
            var values = new List<Single>();
            Single? val;
            while ((val = (Single?)reader.ReadAsDouble()) != null)
            {
                values.Add(val.Value);
            }
            if (values.Count > 0)
            {
                return values.ToArray();
            }
            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, Single[] value, JsonSerializer serializer)
        {
            if (value != null)
            {
                writer.WriteStartArray();
                foreach (var n in value)
                {
                    var c = n;
                    if (Math.Round(c) - c == 0)
                        writer.WriteValue((Int32)c);
                    else writer.WriteValue(c);
                }
                writer.WriteEndArray();
            }

        }
    }
}
