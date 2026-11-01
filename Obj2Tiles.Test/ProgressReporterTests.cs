using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Obj2Tiles.Stages;
using Shouldly;

namespace Obj2Tiles.Test;

/// <summary>The --progress JSON lines: format, ordering under parallel writers, and a stages run.</summary>
public class ProgressReporterTests
{
    private static List<JsonElement> ParseLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .Select(l => JsonDocument.Parse(l).RootElement.Clone())
            .ToList();

    [Test]
    public void FormatStep_hasStageDoneTotalPercent()
    {
        ProgressReporter.FormatStep("decimation", 2, 3, 66.6666, null)
            .ShouldBe("{\"stage\":\"decimation\",\"done\":2,\"total\":3,\"percent\":66.7}");
    }

    [Test]
    public void FormatStep_withLod_putsLodAfterStage()
    {
        ProgressReporter.FormatStep("splitting", 5, 64, 37.5, 0)
            .ShouldBe("{\"stage\":\"splitting\",\"lod\":0,\"done\":5,\"total\":64,\"percent\":37.5}");
    }

    [Test]
    public void FormatDone_and_FormatError()
    {
        ProgressReporter.FormatDone(TimeSpan.FromMilliseconds(12345.6789))
            .ShouldBe("{\"stage\":\"done\",\"percent\":100,\"elapsed\":12.346}");

        // Quotes, backslashes and newlines are escaped; accented text stays readable.
        ProgressReporter.FormatError("file \"C:\\x\" non è\nvalido")
            .ShouldBe("{\"stage\":\"error\",\"message\":\"file \\\"C:\\\\x\\\" non è\\nvalido\"}");
    }

    [Test]
    public void StageCounter_emitsZeroThenEveryStep_andClampsAtTotal()
    {
        var sw = new StringWriter();
        var counter = new ProgressReporter(sw).Begin("tiling", 2);
        counter.Advance();
        counter.Advance();
        counter.Advance(); // spurious extra step must not exceed total

        var lines = ParseLines(sw.ToString());
        lines.Select(l => l.GetProperty("done").GetInt32()).ShouldBe(new[] { 0, 1, 2, 2 });
        lines.Select(l => l.GetProperty("percent").GetDouble()).ShouldBe(new[] { 0, 50, 100, 100.0 });
        lines.ShouldAllBe(l => l.GetProperty("stage").GetString() == "tiling" && l.GetProperty("total").GetInt32() == 2);
    }

    [Test]
    public void StageCounter_parallelAdvances_areWholeLinesWithMonotonicDone()
    {
        const int steps = 2000;
        var sw = new StringWriter();
        var counter = new ProgressReporter(sw).Begin("tiling", steps);

        Parallel.For(0, steps, _ => counter.Advance());

        var lines = ParseLines(sw.ToString());
        lines.Count.ShouldBe(steps + 1);
        var done = lines.Select(l => l.GetProperty("done").GetInt32()).ToArray();
        done.ShouldBe(Enumerable.Range(0, steps + 1).ToArray());
    }

    [Test]
    public void PartsCounter_percentIsMeanOfParts()
    {
        var sw = new StringWriter();
        var counter = new ProgressReporter(sw).BeginParts("splitting", 2);
        counter.SetTotal(0, 4);
        counter.Advance(0);          // part 0: 1/4 -> stage 12.5%
        counter.SetTotal(1, 0);      // an empty part counts as complete -> 62.5%
        counter.Advance(0);
        counter.Advance(0);
        counter.Advance(0);          // 100%

        var lines = ParseLines(sw.ToString());
        lines[0].GetProperty("done").GetInt32().ShouldBe(0);
        lines[0].GetProperty("total").GetInt32().ShouldBe(2);
        lines[0].TryGetProperty("lod", out _).ShouldBeFalse();

        lines[2].GetProperty("lod").GetInt32().ShouldBe(0);
        lines[2].GetProperty("done").GetInt32().ShouldBe(1);
        lines[2].GetProperty("total").GetInt32().ShouldBe(4);
        lines[2].GetProperty("percent").GetDouble().ShouldBe(12.5);
        lines[3].GetProperty("lod").GetInt32().ShouldBe(1);
        lines[3].GetProperty("percent").GetDouble().ShouldBe(62.5);
        lines[^1].GetProperty("percent").GetDouble().ShouldBe(100);
    }

    [Test]
    public async Task Stages_reportDecimationSplittingTiling()
    {
        var outBase = Path.Combine("TestOutput", nameof(Stages_reportDecimationSplittingTiling));
        if (Directory.Exists(outBase)) Directory.Delete(outBase, true);
        var decDir = Directory.CreateDirectory(Path.Combine(outBase, "dec")).FullName;
        var splitDir = Directory.CreateDirectory(Path.Combine(outBase, "split")).FullName;
        var tileDir = Directory.CreateDirectory(Path.Combine(outBase, "tiles")).FullName;

        var input = Path.GetFullPath(Path.Combine("TestData", "quality", "cube-colors-textured",
            "cube-colors-textured.obj"));

        var sw = new StringWriter();
        var progress = new ProgressReporter(sw);
        const int lods = 3;

        var dec = await StagesFacade.Decimate(input, decDir, lods, progress: progress);
        var bounds = await StagesFacade.Split(dec.DestFiles, splitDir, 1, false, isOctree: true, progress: progress);
        StagesFacade.Tile(splitDir, tileDir, lods, null, bounds, localMode: true, isOctree: true, useGlb: true,
            progress: progress);

        var lines = ParseLines(sw.ToString());
        var stages = lines.Select(l => l.GetProperty("stage").GetString()).Distinct().ToArray();
        stages.ShouldBe(new[] { "decimation", "splitting", "tiling" });

        var decimation = lines.Where(l => l.GetProperty("stage").GetString() == "decimation").ToList();
        decimation.Count.ShouldBe(lods + 1);
        decimation[^1].GetProperty("done").GetInt32().ShouldBe(lods);
        decimation[^1].GetProperty("percent").GetDouble().ShouldBe(100);

        var splitting = lines.Where(l => l.GetProperty("stage").GetString() == "splitting").ToList();
        splitting[^1].GetProperty("percent").GetDouble().ShouldBe(100);
        splitting.Skip(1).All(l => l.TryGetProperty("lod", out _)).ShouldBeTrue();
        var splitTiles = bounds.Sum(b => b.Count);
        // tiles written + one "count known" line per LOD + the opening line
        splitting.Count.ShouldBe(splitTiles + lods + 1);

        var tiling = lines.Where(l => l.GetProperty("stage").GetString() == "tiling").ToList();
        tiling[0].GetProperty("total").GetInt32().ShouldBe(splitTiles);
        tiling[^1].GetProperty("done").GetInt32().ShouldBe(splitTiles);
        tiling[^1].GetProperty("percent").GetDouble().ShouldBe(100);

        foreach (var stage in stages)
        {
            var percents = lines.Where(l => l.GetProperty("stage").GetString() == stage)
                .Select(l => l.GetProperty("percent").GetDouble()).ToArray();
            percents.ShouldBe(percents.OrderBy(p => p).ToArray(), $"{stage} percent must not go backwards");
        }
    }
}
