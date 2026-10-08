using System.Globalization;
using NUnit.Framework;
using Shouldly;

namespace Obj2Tiles.Test.Quality;

/// <summary>
/// Parser-level coverage for --scale: decimals and the fraction form advertised by
/// the CLI (e.g. 1200.0/3937.0 for US survey feet), shared by the CLI and the
/// quality-gate expectations via Options.TryParseScale.
/// </summary>
[TestFixture]
[Category("QualityGate")]
public class ScaleParsingTests
{
    [TestCase("1", 1.0)]
    [TestCase(" 1 ", 1.0)]
    [TestCase("0.3048", 0.3048)]
    [TestCase(".5", 0.5)]
    [TestCase("+2", 2.0)]
    [TestCase("1e2", 100.0)]
    [TestCase("1200.0/3937.0", 1200.0 / 3937.0)] // US survey feet -> meters
    [TestCase("3048/10000", 0.3048)]
    [TestCase(" 1 / 2 ", 0.5)]
    [TestCase("1/3", 1.0 / 3.0)] // full double precision, not a rounded decimal
    public void Valid(string raw, double expected)
    {
        Options.TryParseScale(raw, out var scale, out var error).ShouldBeTrue($"'{raw}' should parse: {error}");
        error.ShouldBeNull();
        scale.ShouldBe(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("abc")]
    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("1,5")] // comma is not the invariant decimal separator
    [TestCase("1/0")]
    [TestCase("1200/0.0")]
    [TestCase("1/2/3")]
    [TestCase("1/")]
    [TestCase("/2")]
    [TestCase("-1200/3937")]
    [TestCase("1e999")] // overflow -> not finite
    [TestCase("Infinity")]
    public void Invalid(string? raw)
    {
        Options.TryParseScale(raw, out _, out var error).ShouldBeFalse($"'{raw}' should be rejected");
        error.ShouldNotBeNullOrWhiteSpace($"'{raw}' must produce a user-facing message");
        // Rejections must never leak a usable scale.
    }

    [Test]
    public void Fraction_keeps_full_numerator_denominator_precision()
    {
        // A pre-rounded decimal (0.3048) differs from the exact survey-feet ratio;
        // the fraction form must evaluate it as one division.
        Options.TryParseScale("1200.0/3937.0", out var fraction, out _).ShouldBeTrue();
        fraction.ShouldNotBe(0.3048);
        fraction.ShouldBe(1200.0 / 3937.0);
        (fraction * 3937.0).ShouldBe(1200.0, 1e-9);
    }

    [Test]
    public void Default_options_scale_parses_as_one()
    {
        var options = new Options { Input = "in.obj", Output = "out" };
        options.Scale.ShouldBe("1");
        Options.TryParseScale(options.Scale, out var scale, out _).ShouldBeTrue();
        scale.ShouldBe(1.0);
        // Guard the culture invariant: the machine's decimal separator must not matter.
        CultureInfo.CurrentCulture = new CultureInfo("it-IT");
        try
        {
            Options.TryParseScale("0.3048", out var italian, out _).ShouldBeTrue();
            italian.ShouldBe(0.3048);
        }
        finally
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }
    }
}
