using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Obj2Tiles.Library.Materials;

public class Material : ICloneable
{
    public readonly string Name;
    public string? Texture;
    public string? Texture2;

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

    public Material(string name, string? texture = null, string? texture2 = null, RGB? ambientColor = null, RGB? diffuseColor = null,
        RGB? specularColor = null, double? specularExponent = null, double? dissolve = null,
        IlluminationModel? illuminationModel = null)
    {
        Name = name;
        Texture = texture;
        Texture2 = texture2;
        AmbientColor = ambientColor;
        DiffuseColor = diffuseColor;
        SpecularColor = specularColor;
        SpecularExponent = specularExponent;
        Dissolve = dissolve;
        IlluminationModel = illuminationModel;
    }

    public static Material[] ReadMtl(string path, out string[] dependencies)
    {
        var lines = File.ReadAllLines(path);
        var materials = new List<Material>();
        var deps = new List<string>();

        string texture = null;
        string texture2 = null;

        var name = string.Empty;
        RGB? ambientColor = null, diffuseColor = null, specularColor = null;
        double? specularExponent = null, dissolve = null;
        IlluminationModel? illuminationModel = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(' ');
            switch (parts[0])
            {
                case "newmtl":

                    if (name.Length > 0)
                        materials.Add(new Material(name, texture, texture2, ambientColor, diffuseColor, specularColor,
                            specularExponent, dissolve, illuminationModel));

                    name = parts[1];

                    break;
                case "map_Kd":
                    texture = Path.IsPathRooted(parts[1])
                        ? parts[1]
                        : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, parts[1]));
                    
                    deps.Add(texture);
                    
                    break;
                case "map_Ka":
                    texture2 = Path.IsPathRooted(parts[1])
                        ? parts[1]
                        : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, parts[1]));

                    deps.Add(texture2);

                    break;
                case "Ka":
                    ambientColor = new RGB(
                        double.Parse(parts[1], CultureInfo.InvariantCulture),
                        double.Parse(parts[2], CultureInfo.InvariantCulture),
                        double.Parse(parts[3], CultureInfo.InvariantCulture));
                    break;
                case "Kd":
                    diffuseColor = new RGB(
                        double.Parse(parts[1], CultureInfo.InvariantCulture),
                        double.Parse(parts[2], CultureInfo.InvariantCulture),
                        double.Parse(parts[3], CultureInfo.InvariantCulture));
                    break;
                case "Ks":
                    specularColor = new RGB(
                        double.Parse(parts[1], CultureInfo.InvariantCulture),
                        double.Parse(parts[2], CultureInfo.InvariantCulture),
                        double.Parse(parts[3], CultureInfo.InvariantCulture));
                    break;
                case "Ns":
                    specularExponent = double.Parse(parts[1], CultureInfo.InvariantCulture);
                    break;
                case "d":
                    dissolve = double.Parse(parts[1], CultureInfo.InvariantCulture);
                    break;
                case "Tr":
                    dissolve = 1 - double.Parse(parts[1], CultureInfo.InvariantCulture);
                    break;
                case "illum":
                    illuminationModel = (IlluminationModel)int.Parse(parts[1]);
                    break;
                default:
                    Debug.WriteLine($"Unknown line: '{line}'");
                    break;
            }
        }

        materials.Add(new Material(name, texture, texture2, ambientColor, diffuseColor, specularColor, specularExponent, dissolve,
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
        if (Texture2 != null)
        {
            builder.Append("map_Ka ");
            builder.AppendLine(Texture2.Replace('\\', '/'));
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
            Texture2,
            AmbientColor,
            DiffuseColor,
            SpecularColor,
            SpecularExponent,
            Dissolve,
            IlluminationModel);
    }
}