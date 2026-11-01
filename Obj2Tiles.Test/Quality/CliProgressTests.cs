using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Shouldly;

namespace Obj2Tiles.Test.Quality;

/// <summary>--progress end to end through the real CLI: JSON lines on stdout, absent without the flag.</summary>
[TestFixture]
[Category("QualityGate")]
public class CliProgressTests
{
    private static string[] StdoutLines(CliResult run) =>
        run.Stdout.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    // The --progress events: the stdout lines that start with '{'.
    private static List<JsonElement> ProgressLines(CliResult run) =>
        StdoutLines(run).Where(l => l.StartsWith('{')).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    private static JsonElement LastProgressLine(CliResult run)
    {
        var json = ProgressLines(run);
        json.ShouldNotBeEmpty(run.Tail);
        return json[^1];
    }

    [Test]
    public void Progress_writesJsonLinesEndingWithDone()
    {
        using var dataset = QualityDataset.CubeTextured();
        var dir = Path.Combine(CliHarness.FreshOutputFolder(nameof(Progress_writesJsonLinesEndingWithDone)), "out");

        var run = CliHarness.RunCli(new[] { "--local", "--lods", "3", "-d", "1", "--progress", dataset.InputObj, dir });
        run.ExitCode.ShouldBe(0, run.Tail);

        var lines = StdoutLines(run);
        var json = ProgressLines(run);

        json.ShouldNotBeEmpty();
        json.Select(j => j.GetProperty("stage").GetString()).Distinct()
            .ShouldBe(new[] { "decimation", "splitting", "root", "tiling", "done" });

        var last = json[^1];
        last.GetProperty("stage").GetString().ShouldBe("done");
        last.GetProperty("percent").GetDouble().ShouldBe(100);
        last.GetProperty("elapsed").GetDouble().ShouldBeGreaterThan(0);
        lines.Last(l => l.Length > 0).ShouldStartWith("{\"stage\":\"done\"");

        // Every event is a whole line of its own (never appended to a log line), and the human log is still there.
        lines.Where(l => l.Contains("{\"stage\":")).ShouldAllBe(l => l.StartsWith('{'));
        run.Stdout.ShouldContain(" => Decimation stage with 3 LODs");
    }

    [Test]
    public void WithoutProgress_noJsonLines()
    {
        using var dataset = QualityDataset.CubeTextured();
        var dir = Path.Combine(CliHarness.FreshOutputFolder(nameof(WithoutProgress_noJsonLines)), "out");

        var run = CliHarness.RunCli(new[] { "--local", "--lods", "2", "-d", "1", dataset.InputObj, dir });
        run.ExitCode.ShouldBe(0, run.Tail);
        run.Stdout.Split('\n').ShouldNotContain(l => l.StartsWith('{'));
    }

    [Test]
    public void Progress_failure_endsWithErrorLine()
    {
        var dir = Path.Combine(CliHarness.FreshOutputFolder(nameof(Progress_failure_endsWithErrorLine)), "out");
        var missing = Path.Combine(dir, "does-not-exist.obj");

        var run = CliHarness.RunCli(new[] { "--progress", missing, dir });
        run.ExitCode.ShouldBe(1, run.Tail);

        var j = LastProgressLine(run);
        j.GetProperty("stage").GetString().ShouldBe("error");
        j.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public void Progress_invalidOptions_errorCarriesTheReason()
    {
        using var dataset = QualityDataset.CubeTextured();
        var dir = Path.Combine(CliHarness.FreshOutputFolder(nameof(Progress_invalidOptions_errorCarriesTheReason)), "out");

        var run = CliHarness.RunCli(new[] { "--progress", "--lods", "0", dataset.InputObj, dir });
        run.ExitCode.ShouldBe(1, run.Tail);

        var j = LastProgressLine(run);
        j.GetProperty("stage").GetString().ShouldBe("error");
        j.GetProperty("message").GetString().ShouldBe("LODs must be at least 1");
    }

    [TestCase("out")]
    [TestCase("out.3tz")]
    public void Progress_outputNotCreatable_endsWithErrorLine(string output)
    {
        using var dataset = QualityDataset.CubeTextured();
        var folder = CliHarness.FreshOutputFolder($"{nameof(Progress_outputNotCreatable_endsWithErrorLine)}_{output}");

        // A folder cannot be made under a regular file, on any platform.
        var blocker = Path.Combine(folder, "a-file");
        File.WriteAllText(blocker, "");

        var run = CliHarness.RunCli(new[] { "--progress", dataset.InputObj, Path.Combine(blocker, output) });
        run.ExitCode.ShouldBe(1, run.Tail);
        run.Stderr.ShouldNotContain("Unhandled exception");

        var j = LastProgressLine(run);
        j.GetProperty("stage").GetString().ShouldBe("error");
        j.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
