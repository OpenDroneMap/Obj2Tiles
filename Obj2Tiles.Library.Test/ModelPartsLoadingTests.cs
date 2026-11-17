using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Library.Materials;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Path = System.IO.Path;

namespace Obj2Tiles.Library.Test;

/// <summary>
/// MeshUtils.LoadMesh (the reader of the split stage, so of the finest level) on models whose parts differ:
/// parts with and without texture coordinates, material names with spaces, faces before the first usemtl,
/// vertex colours on some objects only. Every face must reach the mesh with the material it was written with.
/// </summary>
public class ModelPartsLoadingTests
{
    private const string TestOutputPath = "TestOutput";

    private static string Folder(string testName)
    {
        var folder = Path.Combine(TestOutputPath, nameof(ModelPartsLoadingTests), testName);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        return folder;
    }

    // Writes model.obj (and model.mtl and a 16x16 tex.png when mtl is given) and returns the OBJ path.
    private static string Write(string testName, string obj, string? mtl = null)
    {
        var folder = Folder(testName);
        File.WriteAllText(Path.Combine(folder, "model.obj"), obj);

        if (mtl != null)
        {
            File.WriteAllText(Path.Combine(folder, "model.mtl"), mtl);
            using var texture = new Image<Rgba32>(16, 16, new Rgba32(200, 30, 30, 255));
            texture.SaveAsPng(Path.Combine(folder, "tex.png"));
        }

        return Path.Combine(folder, "model.obj");
    }

    // Two unit squares (two triangles each): the first textured with "textured", the second without
    // texture coordinates, with the material given. No texture coordinate of the textured square is (0, 0),
    // the one given to the faces without, so that theirs cannot be mistaken for one of the textured square.
    private static string TexturedAndPlain(string plainMaterialStatement) =>
        "mtllib model.mtl\n" +
        "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\n" +
        "vt 0.1 0.1\nvt 0.9 0.1\nvt 0.9 0.9\nvt 0.1 0.9\n" +
        "v 5 0 0\nv 6 0 0\nv 6 1 0\nv 5 1 0\n" +
        "usemtl textured\nf 1/1 2/2 3/3\nf 1/1 3/3 4/4\n" +
        plainMaterialStatement +
        "f 5 6 7\nf 5 7 8\n";

    private const string TexturedAndPlainMtl =
        "newmtl textured\nKd 1 1 1\nmap_Kd tex.png\n\nnewmtl Paint 1  Carmine\nKd 0.6 0.05 0.1\n";

    private static string[] MaterialOfEachFace(MeshT mesh) =>
        mesh.Faces.Select(f => mesh.Materials[f.MaterialIndex].Name).ToArray();

    [Test]
    public void LoadMesh_FacesWithoutTextureCoordinates_InATexturedModel_AreKeptWithTheirMaterial()
    {
        var path = Write(nameof(LoadMesh_FacesWithoutTextureCoordinates_InATexturedModel_AreKeptWithTheirMaterial),
            TexturedAndPlain("usemtl Paint 1  Carmine\n"), TexturedAndPlainMtl);

        var mesh = MeshUtils.LoadMesh(path).ShouldBeOfType<MeshT>();

        mesh.FacesCount.ShouldBe(4);
        MaterialOfEachFace(mesh).ShouldBe(new[] { "textured", "textured", "Paint 1  Carmine", "Paint 1  Carmine" },
            ignoreOrder: true);
        mesh.Faces.ShouldAllBe(f => f.TextureIndexA < mesh.TextureVertices.Count &&
                                    f.TextureIndexB < mesh.TextureVertices.Count &&
                                    f.TextureIndexC < mesh.TextureVertices.Count);
    }

    [Test]
    public void LoadMesh_MaterialNameWithSpaces_IsOneName()
    {
        var path = Write(nameof(LoadMesh_MaterialNameWithSpaces_IsOneName),
            "mtllib model.mtl\n" +
            "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\n" +
            "usemtl textured\nf 1/1 2/2 3/3\nusemtl   Paint 1  Carmine  \nf 1/1 3/3 4/4\n",
            TexturedAndPlainMtl);

        var mesh = MeshUtils.LoadMesh(path).ShouldBeOfType<MeshT>();

        MaterialOfEachFace(mesh).ShouldBe(new[] { "textured", "Paint 1  Carmine" });
    }

    private const string TwoSquares =
        "mtllib model.mtl\n" +
        "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\n" +
        "vt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\n" +
        "v 5 0 0\nv 6 0 0\nv 6 1 0\nv 5 1 0\n";

    [TestCase("f 1/1 2/2 3/3\nf 1/1 3/3 4/4\n", TestName = "LoadMesh_TexturedFacesBeforeTheFirstUsemtl_GetTheDefaultMaterial")]
    [TestCase("f 1 2 3\nf 1 3 4\n", TestName = "LoadMesh_UntexturedFacesBeforeTheFirstUsemtl_GetTheDefaultMaterial")]
    public void LoadMesh_FacesBeforeTheFirstUsemtl_GetTheDefaultMaterial(string facesWithoutMaterial)
    {
        var path = Write(TestContext.CurrentContext.Test.Name,
            TwoSquares + facesWithoutMaterial + "usemtl textured\nf 5/1 6/2 7/3\nf 5/1 7/3 8/4\n",
            TexturedAndPlainMtl);

        var mesh = MeshUtils.LoadMesh(path).ShouldBeOfType<MeshT>();

        MaterialOfEachFace(mesh).ShouldBe(new[]
            { MeshUtils.DefaultMaterialName, MeshUtils.DefaultMaterialName, "textured", "textured" },
            ignoreOrder: true);
        mesh.Materials.Single(m => m.Name == MeshUtils.DefaultMaterialName).Texture.ShouldBeNull();
    }

    [Test]
    public void LoadMesh_TextureCoordinatesWithoutMaterials_IsAnUntexturedMeshWithEveryFace()
    {
        // As Blender writes objects without material when only the last one has UVs.
        var path = Write(nameof(LoadMesh_TextureCoordinatesWithoutMaterials_IsAnUntexturedMeshWithEveryFace),
            "v 0 0 0\nv 1 0 0\nv 1 1 0\nf 1 2 3\n" +
            "v 5 0 0\nv 6 0 0\nv 6 1 0\nvt 0 0\nvt 1 0\nvt 1 1\nf 4/1 5/2 6/3\n");

        var mesh = MeshUtils.LoadMesh(path).ShouldBeOfType<Mesh>();

        mesh.FacesCount.ShouldBe(2);
        mesh.Faces.ShouldAllBe(f => f.GetType() == typeof(Face));
    }

    [Test]
    public void LoadMesh_SomeVerticesWithoutColours_AreWhite()
    {
        var path = Write(nameof(LoadMesh_SomeVerticesWithoutColours_AreWhite),
            "v 0 0 0\nv 1 0 0\nv 1 1 0\n" +
            "v 5 0 0 1 0 0\nv 6 0 0 0 1 0\nv 6 1 0 0 0 1\n" +
            "v 9 0 0\n" +
            "f 1 2 3\nf 4 5 6\nf 4 6 7\n");

        var mesh = MeshUtils.LoadMesh(path).ShouldBeOfType<Mesh>();

        mesh.VertexColors.ShouldNotBeNull();
        mesh.VertexColors!.Select(c => (c.R, c.G, c.B)).ShouldBe(new[]
        {
            (1d, 1d, 1d), (1d, 1d, 1d), (1d, 1d, 1d),
            (1d, 0d, 0d), (0d, 1d, 0d), (0d, 0d, 1d),
            (1d, 1d, 1d)
        });
    }

    [TestCase(TexturesStrategy.Repack)]
    [TestCase(TexturesStrategy.RepackCompressed)]
    [TestCase(TexturesStrategy.KeepOriginal)]
    public void WriteObj_TexturedModelWithAnUntexturedPart_KeepsEveryFaceAndValidTextureCoordinates(
        TexturesStrategy strategy)
    {
        var testName = nameof(WriteObj_TexturedModelWithAnUntexturedPart_KeepsEveryFaceAndValidTextureCoordinates) +
                       strategy;
        var path = Write(testName, TexturedAndPlain("usemtl Paint 1  Carmine\n"), TexturedAndPlainMtl);
        var mesh = MeshUtils.LoadMesh(path).ShouldBeOfType<MeshT>();
        mesh.TexturesStrategy = strategy;

        var output = Path.Combine(Path.GetDirectoryName(path)!, "out", "Mesh.obj");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        mesh.WriteObj(output);

        var written = File.ReadAllLines(output);
        var textureVertices = written.Count(l => l.StartsWith("vt "));
        foreach (var face in written.Where(l => l.StartsWith("f ")))
        foreach (var corner in face.Split(' ').Skip(1))
            int.Parse(corner.Split('/')[1]).ShouldBeInRange(1, textureVertices, face);

        var reloaded = MeshUtils.LoadMesh(output).ShouldBeOfType<MeshT>();
        MaterialOfEachFace(reloaded).ShouldBe(new[] { "textured", "textured", "Paint 1  Carmine", "Paint 1  Carmine" },
            ignoreOrder: true);
    }
}
