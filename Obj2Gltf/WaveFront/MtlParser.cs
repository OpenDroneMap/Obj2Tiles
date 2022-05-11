using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace SilentWave.Obj2Gltf.WaveFront
{
    public class MtlParser : IMtlParser
    {
        private const String newmtlPrefix = "newmtl";
        private const String KaPrefix = "Ka";
        private const String KdPrefix = "Kd";
        private const String KsPrefix = "Ks";
        private const String KePrefix = "Ke";
        private const String dPrefix = "d";
        private const String TrPrefix = "Tr";
        private const String NsPrefix = "Ns";
        private const String map_kaPrefix = "map_Ka";
        private const String map_KdPrefix = "map_Kd";

        private static Reflectivity GetReflectivity(String val)
        {
            if (String.IsNullOrEmpty(val)) return null;
            var strs = val.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length == 3)
            {
                var r = Double.Parse(strs[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                var g = Double.Parse(strs[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                var b = Double.Parse(strs[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);

                return new Reflectivity(new FactorColor(r, g, b));
            }

            //TODO:
            return null;
        }

        private static Transparency GetTransparency(String str)
        {
            var ok = Double.TryParse(
                str,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture.NumberFormat,
                out var val);
            if (ok)
            {
                return new Transparency { Factor = val };
            }
            return null;
        }

        private static Dissolve GetDissolve(String str)
        {
            var strs = str.Split(new Char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var ok = Double.TryParse(
                strs[strs.Length - 1],
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture.NumberFormat,
                out var val);
            if (!ok) return null;
            if (val == 0)
            {
                val = 1.0f;
            }
            var d = new Dissolve { Factor = val };
            if (strs[0] == "-halo")
            {
                d.Halo = true;
            }
            return d;
        }

        public Task<Material[]> ParseAsync(String path, String searchPath = null, Encoding encoding = null)
        {
            if (searchPath == null) searchPath = Path.GetDirectoryName(path);
            return Task.Run(() =>
            {
                using (var file = File.OpenRead(path))
                {
                    return Parse(file, searchPath, encoding).ToArray();
                }
            });
        }

        public Task<Material[]> ParseAsync(Stream stream, String searchPath, Encoding encoding = null)
            => Task.Run(() => Parse(stream, searchPath, encoding).ToArray());

        public Material[] Parse(String path, String searchPath = null, Encoding encoding = null)
        {
            using (var file = File.OpenRead(path))
            {
                if (searchPath == null) searchPath = Path.GetDirectoryName(path);
                return Parse(file, searchPath, encoding).ToArray();
            }
        }

        public IEnumerable<Material> Parse(Stream stream, String searchPath, Encoding encoding = null)
        {
            if (stream.Position != 0)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
            var reader = encoding != null
                ? new StreamReader(stream, encoding)
                : new StreamReader(stream);
            using (reader)
            {
                Material currentMaterial = null;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine().Trim();
                    if (line.StartsWith(newmtlPrefix))
                    {
                        if (currentMaterial != null) yield return currentMaterial;
                        currentMaterial = new Material
                        {
                            Name = line.Substring(newmtlPrefix.Length).Trim()
                        };
                    }
                    else if (line.StartsWith(KaPrefix))
                    {
                        var ka = line.Substring(KaPrefix.Length).Trim();
                        var r = GetReflectivity(ka);
                        if (r != null)
                        {
                            currentMaterial.Ambient = r;
                        }
                    }
                    else if (line.StartsWith(KdPrefix))
                    {
                        var kd = line.Substring(KdPrefix.Length).Trim();
                        var r = GetReflectivity(kd);
                        if (r != null)
                        {
                            currentMaterial.Diffuse = r;
                        }
                    }
                    else if (line.StartsWith(KsPrefix))
                    {
                        var ks = line.Substring(KsPrefix.Length).Trim();
                        var r = GetReflectivity(ks);
                        if (r != null)
                        {
                            currentMaterial.Specular = r;
                        }
                    }
                    else if (line.StartsWith(KePrefix))
                    {
                        var ks = line.Substring(KePrefix.Length).Trim();
                        var r = GetReflectivity(ks);
                        if (r != null)
                        {
                            currentMaterial.Emissive = r;
                        }
                    }
                    else if (line.StartsWith(dPrefix))
                    {
                        var d = line.Substring(dPrefix.Length).Trim();
                        currentMaterial.Dissolve = GetDissolve(d);
                    }
                    else if (line.StartsWith(TrPrefix))
                    {
                        var tr = line.Substring(TrPrefix.Length).Trim();
                        currentMaterial.Transparency = GetTransparency(tr);
                    }
                    else if (line.StartsWith(NsPrefix))
                    {
                        var ns = line.Substring(NsPrefix.Length).Trim();
                        if (ns.Contains("."))
                        {
                            var d = Single.Parse(ns, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                            currentMaterial.SpecularExponent = (Int32)Math.Round(d);
                        }
                        else
                        {
                            currentMaterial.SpecularExponent = Int32.Parse(ns, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                        }
                    }
                    else if (line.StartsWith(map_kaPrefix))
                    {
                        var ma = line.Substring(map_kaPrefix.Length).Trim();
                        if (File.Exists(Path.Combine(searchPath, ma)))
                        {
                            currentMaterial.AmbientTextureFile = ma;
                        }
                    }
                    else if (line.StartsWith(map_KdPrefix))
                    {
                        var md = line.Substring(map_KdPrefix.Length).Trim();
                        if (File.Exists(Path.Combine(searchPath, md)))
                        {
                            currentMaterial.DiffuseTextureFile = md;
                        }
                    }
                }
                if (currentMaterial != null) yield return currentMaterial;
            }
        }
    }
}
