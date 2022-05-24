using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SilentWave.Obj2Gltf.Gltf
{
    /// <summary>
    /// Convert to int[] when all values are equals integers
    /// </summary>
    public class SingleArrayJsonConverter : JsonConverter<float[]>
    {
        public override float[] ReadJson(JsonReader reader,
                                          Type objectType,
                                          float[] existingValue,
                                          bool hasExistingValue,
                                          JsonSerializer serializer)
        {
            var values = new List<float>();
            float? val;
            while ((val = (float?)reader.ReadAsDouble()) != null)
            {
                values.Add(val.Value);
            }
            if (values.Count > 0)
            {
                return values.ToArray();
            }
            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, float[] value, JsonSerializer serializer)
        {
            if (value != null)
            {
                writer.WriteStartArray();
                foreach (var n in value)
                {
                    var c = n;
                    if (Math.Round(c) - c == 0)
                        writer.WriteValue((int)c);
                    else writer.WriteValue(c);
                }
                writer.WriteEndArray();
            }

        }
    }
}
