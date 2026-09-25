using System.IO;
using CommandLine;
using NUnit.Framework;
using Shouldly;

namespace Obj2Tiles.Test;

public class PresetTests
{
    private static Options ParseWithPreset(params string[] args)
    {
        Options? options = null;
        using var parser = new Parser(settings =>
        {
            settings.HelpWriter = null;
            settings.CaseInsensitiveEnumValues = true;
        });
        var result = parser.ParseArguments<Options>(args);
        result.WithParsed(parsed => options = parsed);
        result.Tag.ShouldBe(ParserResultType.Parsed);
        Program.ApplyPreset(options!, args);
        return options!;
    }

    [Test]
    public void Legacy_DisablesZSplitAndOctree()
    {
        var opts = ParseWithPreset("--preset", "legacy", "in.obj", "out");
        opts.EffectiveZSplit.ShouldBeFalse();
        opts.EffectiveOctree.ShouldBeFalse();
    }

    [Test]
    public void Legacy_ExplicitZSplitAndOctree_Win()
    {
        var opts = ParseWithPreset("--preset", "legacy", "--zsplit", "--octree", "in.obj", "out");
        opts.EffectiveZSplit.ShouldBeTrue();
        opts.EffectiveOctree.ShouldBeTrue();
    }

    [Test]
    public void Legacy_ExplicitShortZSplit_Wins()
    {
        ParseWithPreset("--preset", "legacy", "-z", "in.obj", "out").EffectiveZSplit.ShouldBeTrue();
    }

    [Test]
    public void Standard_DefaultsToLocalMode()
    {
        ParseWithPreset("--preset", "standard", "in.obj", "out").LocalMode.ShouldBeTrue();
    }

    [Test]
    public void Standard_ExplicitCoordinates_KeepGeoreferencing()
    {
        var opts = ParseWithPreset("--preset", "standard", "--lat", "45.46", "--lon", "9.19", "in.obj", "out");
        opts.LocalMode.ShouldBeFalse();
        opts.EffectiveUseGlb.ShouldBeTrue();
    }
}

public class ErrorOptionTests
{
    private string _input = null!;

    [SetUp]
    public void Setup()
    {
        _input = Path.GetTempFileName();
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(_input);
    }

    private Options Parse(params string[] extra)
    {
        Options? options = null;
        using var parser = new Parser(settings => settings.HelpWriter = null);
        var result = parser.ParseArguments<Options>([.. extra, _input, "out"]);
        result.WithParsed(parsed => options = parsed);
        result.Tag.ShouldBe(ParserResultType.Parsed);
        return options!;
    }

    [Test]
    public void ZeroError_MeansAuto()
    {
        var opts = Parse("--error", "0");
        Program.CheckOptions(opts).ShouldBeTrue();
        opts.BaseError.ShouldBeNull();
    }

    [Test]
    public void PositiveError_IsKept()
    {
        var opts = Parse("--error", "12.5");
        Program.CheckOptions(opts).ShouldBeTrue();
        opts.BaseError.ShouldBe(12.5);
    }

    [TestCase("--error=-1")]
    [TestCase("--error-factor=0")]
    [TestCase("--overlap=-0.1")]
    public void InvalidValues_AreRejected(string arg)
    {
        Program.CheckOptions(Parse(arg)).ShouldBeFalse();
    }
}
