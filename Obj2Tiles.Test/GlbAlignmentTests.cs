using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Shouldly;
using SilentWave.Obj2Gltf;

namespace Obj2Tiles.Test;

/// <summary>
/// Regression tests for glTF/GLB buffer alignment produced by <see cref="Gltf2GlbConverter"/>.
/// The glTF 2.0 spec requires that <c>accessor.byteOffset + bufferView.byteOffset</c> be a multiple
/// of the accessor's component size. When the per-attribute buffers are merged into a single GLB
/// binary chunk, a buffer whose length is not a multiple of 4 (e.g. an odd count of UNSIGNED_SHORT
/// indices) used to push every following bufferView to a misaligned offset, which the 3D Tiles /
/// glTF validators reject with "Accessor's total byteOffset ... isn't a multiple of componentType
/// length 4."
/// </summary>
public class GlbAlignmentTests
{
    private const string TestOutputPath = "TestOutput";

    // glTF componentType GL constant -> byte size.
    private static readonly Dictionary<int, int> ComponentSize = new()
    {
        [5120] = 1, // BYTE
        [5121] = 1, // UNSIGNED_BYTE
        [5122] = 2, // SHORT
        [5123] = 2, // UNSIGNED_SHORT
        [5125] = 4, // UNSIGNED_INT
        [5126] = 4  // FLOAT
    };

    [SetUp]
    public void Setup() => Directory.CreateDirectory(TestOutputPath);

    private static string GetTestOutputPath(string testName)
    {
        var folder = Path.Combine(TestOutputPath, testName);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string BuildGlbFromObj(string testName, string objContent)
    {
        var dir = GetTestOutputPath(testName);
        var objPath = Path.Combine(dir, "model.obj");
        File.WriteAllText(objPath, objContent);

        var gltfPath = Path.Combine(dir, "model.gltf");
        Converter.MakeDefault().Convert(objPath, gltfPath);

        new Gltf2GlbConverter().Convert(new Gltf2GlbOptions(gltfPath));
        return Path.ChangeExtension(gltfPath, ".glb");
    }

    private static JObject ExtractGltfJson(string glbPath)
    {
        var glb = File.ReadAllBytes(glbPath);
        // GLB layout: header magic(4) version(4) length(4), then JSON chunk header length(4) type(4)
        // followed by the JSON chunk payload.
        var jsonLength = (int)BitConverter.ToUInt32(glb, 12);
        var json = Encoding.UTF8.GetString(glb, 20, jsonLength);
        return JObject.Parse(json);
    }

    private static void AssertAllAccessorsAligned(JObject gltf)
    {
        var bufferViews = (JArray?)gltf["bufferViews"];
        bufferViews.ShouldNotBeNull();
        var accessors = (JArray?)gltf["accessors"];
        accessors.ShouldNotBeNull();

        foreach (var acc in accessors)
        {
            var bvIndex = (int)acc["bufferView"]!;
            var accOffset = (long?)acc["byteOffset"] ?? 0;
            var bvOffset = (long?)bufferViews[bvIndex]["byteOffset"] ?? 0;
            var componentType = (int)acc["componentType"]!;
            var size = ComponentSize[componentType];
            var total = accOffset + bvOffset;

            (total % size).ShouldBe(0,
                $"accessor total byteOffset {total} must be a multiple of component size {size}");
        }
    }

    // A single triangle produces 3 UNSIGNED_SHORT indices => a 6-byte index buffer (6 % 4 == 2).
    // Before the alignment fix, the Positions bufferView started at that odd offset and every FLOAT
    // accessor after it was misaligned.
    [Test]
    public void Gltf2Glb_SingleTriangleOddIndexCount_AllAccessorsAligned()
    {
        const string obj = "v 0 0 0\nv 1 0 0\nv 0 1 0\nvn 0 0 1\nf 1//1 2//1 3//1\n";
        var glb = BuildGlbFromObj(nameof(Gltf2Glb_SingleTriangleOddIndexCount_AllAccessorsAligned), obj);

        var gltf = ExtractGltfJson(glb);

        // Precondition: the index bufferView really is not 4-byte aligned in length, so this test
        // exercises exactly the path that used to produce misaligned downstream accessors.
        var bufferViews = (JArray?)gltf["bufferViews"];
        bufferViews.ShouldNotBeNull();
        var indexBufferView = bufferViews
            .First(bv => (int?)bv["target"] == 34963); // ELEMENT_ARRAY_BUFFER
        ((long)indexBufferView["byteLength"]! % 4).ShouldNotBe(0);

        AssertAllAccessorsAligned(gltf);
    }

    // A triangle fan of three triangles produces 9 indices => an 18-byte index buffer (18 % 4 == 2),
    // another odd-length case that also exercises multiple primitives sharing the index buffer.
    [Test]
    public void Gltf2Glb_MultiTriangleOddIndexCount_AllAccessorsAligned()
    {
        const string obj =
            "v 0 0 0\nv 1 0 0\nv 2 0 0\nv 2 1 0\nv 0 1 0\n" +
            "f 1 2 5\nf 2 3 4\nf 2 4 5\n";
        var glb = BuildGlbFromObj(nameof(Gltf2Glb_MultiTriangleOddIndexCount_AllAccessorsAligned), obj);

        var gltf = ExtractGltfJson(glb);
        AssertAllAccessorsAligned(gltf);
    }
}
