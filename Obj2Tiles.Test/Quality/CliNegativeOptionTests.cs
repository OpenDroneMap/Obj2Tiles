using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace Obj2Tiles.Test.Quality;

/// <summary>
/// Negative configuration matrix: every Program.CheckOptions rule is exercised through the
/// real process (not reflection) and must be rejected with exit code 1 before any pipeline
/// work happens. These are curated, not PICT-generated: they encode the validation contract
/// deliberately, including the dependency constraints the positive matrix excludes.
/// </summary>
[TestFixture]
[Category("QualityGate")]
public class CliNegativeOptionTests
{
    private static readonly Lazy<List<NegativeCase>> Cases = new(QualityFixtures.LoadNegatives);

    private static IEnumerable<TestCaseData> NegativeCases =>
        Cases.Value.Select(c => new TestCaseData(c).SetName(c.Name));

    [TestCaseSource(nameof(NegativeCases))]
    public void InvalidOptionCombination_isRejectedWithExitCode(NegativeCase negativeCase)
    {
        var outBase = CliHarness.FreshOutputFolder("negative-" + negativeCase.Name);
        var input = Path.Combine(CliHarness.TestDataRoot, "quality", "cube-colors-textured", "cube-colors-textured.obj");
        File.Exists(input).ShouldBeTrue("textured cube fixture missing");

        var args = negativeCase.Args
            .Select(a => a
                .Replace("{input}", input)
                .Replace("{noinput}", Path.Combine(outBase, "does-not-exist.obj"))
                .Replace("{outdir}", Path.Combine(outBase, "out")))
            .ToList();

        var run = CliHarness.RunCli(args, timeout: TimeSpan.FromMinutes(2), workingDir: outBase);

        run.ExitCode.ShouldBe(1,
            $"{negativeCase.Name} was expected to be rejected (exit 1)\n--- output tail ---\n{run.Tail}");

        // A rejected run must not have produced a tileset.
        var leftovers = Directory.Exists(Path.Combine(outBase, "out"))
            ? Directory.GetFiles(Path.Combine(outBase, "out"), "tileset.json", SearchOption.AllDirectories)
            : Array.Empty<string>();
        leftovers.ShouldBeEmpty($"{negativeCase.Name} rejected but wrote a tileset.json");
    }
}
