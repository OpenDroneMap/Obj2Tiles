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
        private const string newmtlPrefix = "newmtl";
        private const string KaPrefix = "Ka";
        private const string KdPrefix = "Kd";
        private const string KsPrefix = "Ks";
        private const string KePrefix = "Ke";
        private const string dPrefix = "d";
        private const string TrPrefix = "Tr";
        private const string NsPrefix = "Ns";
        private const string map_kaPrefix = "map_Ka";
        private const string map_KdPrefix = "map_Kd";
        private const string normPrefix = "norm";

        private static Reflectivity GetReflectivity(string val)
        {
            if (string.IsNullOrEmpty(val)) return null;
            var strs = val.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length == 3)
            {
                var r = double.Parse(strs[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                var g = double.Parse(strs[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                var b = double.Parse(strs[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);

                return new Reflectivity(new FactorColor(r, g, b));
            }

            //TODO:
            return null;
        }

        private static Transparency GetTransparency(string str)
        {
            var ok = double.TryParse(
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

        private static Dissolve GetDissolve(string str)
        {
            var strs = str.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var ok = double.TryParse(
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

        public Task<Material[]> ParseAsync(string path, string searchPath = null, Encoding encoding = null)
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

        public Task<Material[]> ParseAsync(Stream stream, string searchPath, Encoding encoding = null)
            => Task.Run(() => Parse(stream, searchPath, encoding).ToArray());

        public Material[] Parse(string path, string searchPath = null, Encoding encoding = null)
        {
            using (var file = File.OpenRead(path))
            {
                if (searchPath == null) searchPath = Path.GetDirectoryName(path);
                return Parse(file, searchPath, encoding).ToArray();
            }
        }

        public IEnumerable<Material> Parse(Stream stream, string searchPath, Encoding encoding = null)
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
                            var d = float.Parse(ns, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                            currentMaterial.SpecularExponent = (int)Math.Round(d);
                        }
                        else
                        {
                            currentMaterial.SpecularExponent = int.Parse(ns, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
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
                    else if (line.StartsWith(normPrefix))
                    {
                        var mn = line.Substring(normPrefix.Length).Trim();
                        if (File.Exists(Path.Combine(searchPath, mn)))
                        {
                            currentMaterial.NormalTextureFile = mn;
                        }
                    }
                }
                if (currentMaterial != null) yield return currentMaterial;
            }
        }
    }
}
