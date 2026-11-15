using System;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Shouldly;

namespace Obj2Tiles.Test;

// The KTX2 encoder hands libktx a ktxBasisParams it declares itself (Ktx2Encoder.KtxBasisParams), and
// libktx refuses one whose structSize is not its own sizeof. These are the size and offsets of the struct
// of the vendored libktx (5.0: the codec replaces the uastc flag, UASTC HDR fields at the end), so a
// change of the library that moves a field fails here instead of in the encoder.
public class Ktx2BasisParamsLayoutTests
{
    private static Type ParamsType()
    {
        var encoder = typeof(SilentWave.Obj2Gltf.GltfConverterOptions).Assembly.GetType("SilentWave.Obj2Gltf.Ktx2.Ktx2Encoder");
        encoder.ShouldNotBeNull();
        var type = encoder.GetNestedType("KtxBasisParams", BindingFlags.NonPublic);
        type.ShouldNotBeNull();
        return type;
    }

    [Test]
    public void TheParamsHaveTheSizeOfLibktx50()
    {
        Marshal.SizeOf(ParamsType()).ShouldBe(96);
    }

    [TestCase("codec", 4)]
    [TestCase("verbose", 8)]
    [TestCase("threadCount", 12)]
    [TestCase("etc1sCompressionLevel", 16)]
    [TestCase("qualityLevel", 20)]
    [TestCase("uastcFlags", 52)]
    [TestCase("uastcRDONoMultithreading", 77)]
    [TestCase("uastcHDRQuality", 80)]
    [TestCase("uastcHDRLambda", 88)]
    [TestCase("uastcHDRLevel", 92)]
    public void EachFieldIsWhereLibktx50ReadsIt(string field, int offset)
    {
        Marshal.OffsetOf(ParamsType(), field).ToInt32().ShouldBe(offset);
    }
}
