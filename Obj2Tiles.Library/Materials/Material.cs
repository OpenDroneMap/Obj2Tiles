using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Obj2Tiles.Library.Materials;

public class Material
{
    public string Name;
    public string? Texture;

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

    public Material(string name, string? texture = null, RGB? ambientColor = null, RGB? diffuseColor = null,
        RGB? specularColor = null, double? specularExponent = null, double? dissolve = null,
        IlluminationModel? illuminationModel = null)
    {
        Name = name;
        Texture = texture;
        AmbientColor = ambientColor;
        DiffuseColor = diffuseColor;
        SpecularColor = specularColor;
        SpecularExponent = specularExponent;
        Dissolve = dissolve;
        IlluminationModel = illuminationModel;
    }

    public static Material[] ReadMtl(string path)
    {
        var lines = File.ReadAllLines(path);
        var materials = new List<Material>();

        string texture = null;
        string name = null;
        RGB ambientColor = null, diffuseColor = null, specularColor = null;
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
                    
                    if (name != null)
                        materials.Add(new Material(name, texture, ambientColor, diffuseColor, specularColor, specularExponent, dissolve, illuminationModel));

                    name = parts[1];
                    
                    break;
                case "map_Kd":
                    texture = Path.IsPathRooted(parts[1]) ? parts[1] : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), parts[1]));
                    break;
                case "Ka":
                    ambientColor = new RGB(
                        double.Parse(parts[1], en), 
                        double.Parse(parts[2], en), 
                        double.Parse(parts[3], en));
                    break;
                case "Kd":
                    diffuseColor = new RGB(
                        double.Parse(parts[1], en), 
                        double.Parse(parts[2], en), 
                        double.Parse(parts[3], en));
                    break;
                case "Ks":
                    specularColor = new RGB(
                        double.Parse(parts[1], en), 
                        double.Parse(parts[2], en), 
                        double.Parse(parts[3], en));
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
                    illuminationModel = (IlluminationModel) int.Parse(parts[1]);
                    break;
                default:
                    Debug.WriteLine($"Unknown line: '{line}'");
                    break;
            }
        }
        
        materials.Add(new Material(name, texture, ambientColor, diffuseColor, specularColor, specularExponent, dissolve, illuminationModel));

        return materials.ToArray();
    }
    
    private static readonly CultureInfo en = CultureInfo.GetCultureInfo("en-US");

    public string ToMtl()
    {
        var builder = new StringBuilder();

        builder.Append("newmtl ");
        builder.AppendLine(Name);

        if (Texture != null)
        {
            builder.Append("map_Kd ");
            builder.AppendLine(Texture.Replace('\\','/'));
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
            builder.AppendLine(SpecularExponent.Value.ToString(en));
        }

        if (Dissolve != null)
        {
            builder.Append("d ");
            builder.AppendLine(Dissolve.Value.ToString(en));
        }

        if (IlluminationModel != null)
        {
            builder.Append("illum ");
            builder.AppendLine(((int)IlluminationModel).ToString());
        }

        return builder.ToString();
    }
}