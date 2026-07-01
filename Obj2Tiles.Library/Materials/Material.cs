using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Obj2Tiles.Library.Materials;

public class Material : ICloneable
{
    public readonly string Name;
    public string? Texture;
    public string? NormalMap;

    private static readonly Dictionary<string, int> MtlOptionValueCounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["-blendu"] = 1, ["-blendv"] = 1, ["-bm"] = 1, ["-boost"] = 1,
        ["-cc"] = 1, ["-clamp"] = 1, ["-imfchan"] = 1, ["-mm"] = 2,
        ["-texres"] = 1, ["-type"] = 1,
        // -o, -s, -t take 1-3 numeric values and are handled separately
    };

    // Strips leading MTL option arguments (e.g. "-bm 1.0 -s 1 1") and returns
    // the texture path at the end of the line, with separators normalized for the
    // current platform. Handles paths that contain spaces.
    private static string ExtractTexturePath(string lineRemainder)
    {
        var tokens = lineRemainder.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int i = 0;
        while (i < tokens.Length && tokens[i].StartsWith('-'))
        {
            var option = tokens[i];
            i++;
            if (option.Equals("-o", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("-t", StringComparison.OrdinalIgnoreCase))
            {
                int consumed = 0;
                while (i < tokens.Length && consumed < 3 &&
                       double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                { i++; consumed++; }
            }
            else
            {
                MtlOptionValueCounts.TryGetValue(option, out var cnt);
                if (cnt == 0) cnt = 1;
                i = Math.Min(i + cnt, tokens.Length);
            }
        }
        var raw = string.Join(' ', tokens[i..]);
        return raw.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    // Tries to locate a texture file by progressively relaxing the base directory.
    // Returns the full absolute path on success, null if the file cannot be found.
    private static string? ResolvePath(string path, string mtlFolder, string objFolder)
    {
        string candidate;

        candidate = Path.GetFullPath(Path.Combine(mtlFolder, path));
        if (File.Exists(candidate)) return candidate;

        if (!string.Equals(mtlFolder, objFolder, StringComparison.OrdinalIgnoreCase))
        {
            candidate = Path.GetFullPath(Path.Combine(objFolder, path));
            if (File.Exists(candidate)) return candidate;
        }

        var fileName = Path.GetFileName(path);
        if (fileName.Length < path.Length)
        {
            candidate = Path.GetFullPath(Path.Combine(mtlFolder, fileName));
            if (File.Exists(candidate)) return candidate;

            if (!string.Equals(mtlFolder, objFolder, StringComparison.OrdinalIgnoreCase))
            {
                candidate = Path.GetFullPath(Path.Combine(objFolder, fileName));
                if (File.Exists(candidate)) return candidate;
            }
        }

        candidate = Path.GetFullPath(path);
        if (File.Exists(candidate)) return candidate;

        return null;
    }

    /// <summary>
    /// Ka - Ambient
    /// </summary>
    public readonly RGB? AmbientColor;

    /// <summary>
    /// Kd - Diffuse
    /// </summary>
    public readonly RGB? DiffuseColor;

    /// <summary>
    /// Ks - Specular
    /// </summary>
    public readonly RGB? SpecularColor;

    /// <summary>
    /// Ns - Specular exponent
    /// </summary>
    public readonly double? SpecularExponent;

    /// <summary>
    /// d - Dissolve / Transparency (Tr = 1 - d)
    /// </summary>
    public readonly double? Dissolve;

    public readonly IlluminationModel? IlluminationModel;

    public Material(string name, string? texture = null, string? normalMap = null, RGB? ambientColor = null, RGB? diffuseColor = null,
        RGB? specularColor = null, double? specularExponent = null, double? dissolve = null,
        IlluminationModel? illuminationModel = null)
    {
        Name = name;
        Texture = texture;
        NormalMap = normalMap;
        AmbientColor = ambientColor;
        DiffuseColor = diffuseColor;
        SpecularColor = specularColor;
        SpecularExponent = specularExponent;
        Dissolve = dissolve;
        IlluminationModel = illuminationModel;
    }

    public static Material[] ReadMtl(string path, out string[] dependencies, string? objFilePath = null)
    {
        var lines = File.ReadAllLines(path);
        var materials = new List<Material>();
        var deps = new List<string>();

        string? texture = null;
        string? normalMap = null;
        var name = string.Empty;
        RGB? ambientColor = null, diffuseColor = null, specularColor = null;
        double? specularExponent = null, dissolve = null;
        IlluminationModel? illuminationModel = null;

        var mtlFolder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
        var objFolder = objFilePath != null
            ? (Path.GetDirectoryName(Path.GetFullPath(objFilePath)) ?? string.Empty)
            : mtlFolder;

        foreach (var line in lines)
        {
            if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                continue;

            var lineTrimmed = line.Trim();
            var spaceIdx = lineTrimmed.IndexOf(' ');
            var keyword = spaceIdx >= 0 ? lineTrimmed[..spaceIdx] : lineTrimmed;
            var remainder = spaceIdx >= 0 ? lineTrimmed[(spaceIdx + 1)..] : string.Empty;
            var parts = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            switch (keyword)
            {
                case "newmtl":
                    if (name.Length > 0)
                        materials.Add(new Material(name, texture, normalMap, ambientColor, diffuseColor, specularColor,
                            specularExponent, dissolve, illuminationModel));

                    name = remainder.Trim();
                    texture = null;
                    normalMap = null;
                    ambientColor = null;
                    diffuseColor = null;
                    specularColor = null;
                    specularExponent = null;
                    dissolve = null;
                    illuminationModel = null;
                    break;

                case "map_Kd":
                {
                    var texPath = ExtractTexturePath(remainder);
                    if (!string.IsNullOrEmpty(texPath))
                    {
                        texture = ResolvePath(texPath, mtlFolder, objFolder);
                        if (texture != null) deps.Add(texture);
                    }
                    break;
                }
                case "norm":
                {
                    var texPath = ExtractTexturePath(remainder);
                    if (!string.IsNullOrEmpty(texPath))
                    {
                        normalMap = ResolvePath(texPath, mtlFolder, objFolder);
                        if (normalMap != null) deps.Add(normalMap);
                    }
                    break;
                }
                case "Ka" when parts.Length >= 3:
                    ambientColor = new RGB(
                        double.Parse(parts[0], CultureInfo.InvariantCulture),
                        double.Parse(parts[1], CultureInfo.InvariantCulture),
                        double.Parse(parts[2], CultureInfo.InvariantCulture));
                    break;
                case "Kd" when parts.Length >= 3:
                    diffuseColor = new RGB(
                        double.Parse(parts[0], CultureInfo.InvariantCulture),
                        double.Parse(parts[1], CultureInfo.InvariantCulture),
                        double.Parse(parts[2], CultureInfo.InvariantCulture));
                    break;
                case "Ks" when parts.Length >= 3:
                    specularColor = new RGB(
                        double.Parse(parts[0], CultureInfo.InvariantCulture),
                        double.Parse(parts[1], CultureInfo.InvariantCulture),
                        double.Parse(parts[2], CultureInfo.InvariantCulture));
                    break;
                case "Ns" when parts.Length >= 1:
                    specularExponent = double.Parse(parts[0], CultureInfo.InvariantCulture);
                    break;
                case "d" when parts.Length >= 1:
                    dissolve = double.Parse(parts[0], CultureInfo.InvariantCulture);
                    break;
                case "Tr" when parts.Length >= 1:
                    dissolve = 1 - double.Parse(parts[0], CultureInfo.InvariantCulture);
                    break;
                case "illum" when parts.Length >= 1:
                    illuminationModel = (IlluminationModel)int.Parse(parts[0]);
                    break;
                default:
                    Debug.WriteLine($"Unknown line: '{line}'");
                    break;
            }
        }

        materials.Add(new Material(name, texture, normalMap, ambientColor, diffuseColor, specularColor, specularExponent, dissolve,
            illuminationModel));

        dependencies = deps.ToArray();

        return materials.ToArray();
    }

    public string ToMtl()
    {
        var builder = new StringBuilder();

        builder.Append("newmtl ");
        builder.AppendLine(Name);

        if (Texture != null)
        {
            builder.Append("map_Kd ");
            builder.AppendLine(Texture.Replace('\\', '/'));
        }
        if (NormalMap != null)
        {
            builder.Append("norm ");
            builder.AppendLine(NormalMap.Replace('\\', '/'));
        }

        if (AmbientColor != null)
        {
            builder.Append("Ka ");
            builder.AppendLine(AmbientColor.ToString());
        }

        if (DiffuseColor != null)
        {
            builder.Append("Kd ");
            builder.AppendLine(DiffuseColor.ToString());
        }

        if (SpecularColor != null)
        {
            builder.Append("Ks ");
            builder.AppendLine(SpecularColor.ToString());
        }

        if (SpecularExponent != null)
        {
            builder.Append("Ns ");
            builder.AppendLine(SpecularExponent.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (Dissolve != null)
        {
            builder.Append("d ");
            builder.AppendLine(Dissolve.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (IlluminationModel != null)
        {
            builder.Append("illum ");
            builder.AppendLine(((int)IlluminationModel).ToString());
        }

        return builder.ToString();
    }

    public object Clone()
    {
        return new Material(
            Name,
            Texture,
            NormalMap,
            AmbientColor,
            DiffuseColor,
            SpecularColor,
            SpecularExponent,
            Dissolve,
            IlluminationModel);
    }
}