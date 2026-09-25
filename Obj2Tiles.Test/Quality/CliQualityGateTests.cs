using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Obj2Tiles.Stages;
using Shouldly;

namespace Obj2Tiles.Test.Quality;

/// <summary>
/// Quality gate: every PICT-generated CLI combination is executed as the real process and
/// its output is checked twice - against the structural/option-contract invariants of
/// TilesetInspector and against the official CesiumGS 3D Tiles validator through the
/// tools/validator harness (allowlisted warnings only).
/// </summary>
[TestFixture]
[Category("QualityGate")]
public class CliQualityGateTests
{
    private static readonly Lazy<List<MatrixCase>> Matrix = new(QualityFixtures.LoadMatrix);

    private static IEnumerable<TestCaseData> MatrixCases =>
        Matrix.Value.Select(c => new TestCaseData(c).SetName(c.Id));

    [TestCaseSource(nameof(MatrixCases))]
    public void CliCombination_producesValidQualityTileset(MatrixCase matrixCase)
    {
        var expected = matrixCase.Expectations;
        using var dataset = QualityFixtures.DatasetFor(matrixCase.Dataset);

        RunValidateAndAssert(matrixCase.Id, dataset, matrixCase.Args, expected);
    }

    /// <summary>
    /// Executes the CLI exactly like a user would, then runs the full gate on the output.
    /// Shared between the PICT matrix and the curated real-world cases.
    /// </summary>
    public static void RunValidateAndAssert(
        string caseId, QualityDataset dataset, IEnumerable<string> cliArgs, QualityExpectations expected)
    {
        var outBase = CliHarness.FreshOutputFolder(caseId);
        var outPath = Path.Combine(outBase, expected.Output3tz ? "out.3tz" : "out");

        var args = cliArgs.ToList();
        args.Add(dataset.InputObj);
        args.Add(outPath);

        var run = CliHarness.RunCli(args, timeout: TimeSpan.FromMinutes(15));
        run.ExitCode.ShouldBe(0, $"obj2tiles failed for {caseId}\n--- output tail ---\n{run.Tail}");

        switch (expected.Stage)
        {
            case Stage.Decimation:
            case Stage.Splitting:
                ExpectIntermediateObjOutput(caseId, outPath);
                return;
            default:
                ExpectTilingOutput(caseId, outBase, outPath, expected, run);
                break;
        }
    }

    private static void ExpectIntermediateObjOutput(string caseId, string outPath)
    {
        Directory.Exists(outPath).ShouldBeTrue($"{caseId}: stage output folder was not created");

        var objs = Directory.GetFiles(outPath, "*.obj", SearchOption.AllDirectories);
        objs.Length.ShouldNotBe(0, $"{caseId}: stage produced no OBJ output");
        objs.Any(f => new FileInfo(f).Length > 0).ShouldBeTrue($"{caseId}: all OBJ outputs are empty");

        File.Exists(Path.Combine(outPath, "tileset.json")).ShouldBeFalse(
            $"{caseId}: early-stop stage must not produce a tileset");
    }

    private static void ExpectTilingOutput(string caseId, string outBase, string outPath, QualityExpectations expected, CliResult run)
    {
        var target = expected.Output3tz ? outPath : Path.Combine(outPath, "tileset.json");
        File.Exists(expected.Output3tz ? outPath : target).ShouldBeTrue(
            $"{caseId}: expected output missing: {target}\n--- output tail ---\n{run.Tail}");

        var problems = TilesetInspector.Inspect(expected.Output3tz ? outPath : outPath, expected);
        problems.ShouldBeEmpty($"{caseId}: quality-gate violations:\n  - " + string.Join("\n  - ", problems));

        ValidateWithOfficialValidator(caseId, outBase, target);
    }

    private static void ValidateWithOfficialValidator(string caseId, string outBase, string tilesetTarget)
    {
        var unavailable = CliHarness.ValidatorUnavailableReason;
        if (unavailable != null)
        {
            TestContext.Progress.WriteLine($"[{caseId}] official validator skipped: {unavailable}");
            Assert.Warn($"official 3D Tiles validation skipped: {unavailable}");
            return;
        }

        var report = Path.Combine(outBase, "validator-report.json");
        var result = CliHarness.RunValidator(tilesetTarget, report);

        // The validator prints the triaged issues (including the allowlisted ones) itself;
        // surface them through Shouldly messages instead of swallowing the process output.
        result.ExitCode.ShouldBe(0,
            $"official 3D Tiles validation failed for {caseId} (allowlist: tools/validator/allowlist.json)\n" +
            $"--- validator output ---\n{result.Tail}\n(report: {report})");
    }
}

/// <summary>
/// Curated runs with the "real world" datasets from test_data (local via OBJ2TILES_TEST_DATA,
/// otherwise download-cached). The odm model is the heavy photogrammetry scene and runs only
/// when OBJ2TILES_RUN_LARGE=1 (nightly in CI).
/// </summary>
[TestFixture]
[Category("QualityGate")]
public class RealWorldQualityTests
{
    private static IEnumerable<TestCaseData> BrightonCases
    {
        get
        {
            yield return Case("brighton-default", new[] { "--lods", "2", "--local" },
                e => { e.Lods = 2; e.Local = true; });
            yield return Case("brighton-decimation-only", new[] { "--stage", "Decimation", "--lods", "2" },
                e => { e.Stage = Stage.Decimation; e.Lods = 2; });
            yield return Case("brighton-splitting-only", new[] { "--stage", "Splitting", "--divisions", "2" },
                e => { e.Stage = Stage.Splitting; });
            yield return Case("brighton-octree", new[] { "--octree", "--lods", "3", "--local" },
                e => { e.Octree = true; e.Local = true; });
            yield return Case("brighton-webp-single-material", new[] { "--texture-format", "Webp", "--single-material-per-part", "--local" },
                e => { e.TextureFormat = QualityTextureFormat.Webp; e.SingleMaterialPerPart = true; e.Local = true; });
            yield return Case("brighton-3tz", new[] { "--texture-quality", "90", "--local" },
                e => { e.Output3tz = true; e.Local = true; }, out3tz: true);
            yield return Case("brighton-zsplit-median", new[] { "--zsplit", "--split-strategy", "VertexMedian", "--local" },
                e => { e.ZSplit = true; e.Local = true; });
            yield return Case("brighton-georef", new[] { "--lat", "45.523", "--lon", "-122.511", "--alt", "12", "--lods", "2" },
                e => { e.Lods = 2; });
            // Fraction --scale (survey-feet style) end-to-end: the georeference transform must
            // carry the evaluated 0.3048 uniform scale (inspector: rotation column norms).
            yield return Case("brighton-scale-fraction", new[] { "--scale", "3048/10000", "--lat", "45.523", "--lon", "-122.511", "--alt", "12", "--lods", "2" },
                e => { e.Lods = 2; e.Scale = 0.3048; });
        }
    }

    private static IEnumerable<TestCaseData> OdmCases
    {
        get
        {
            yield return Case("odm-default", new[] { "--max-texture-size", "1024", "--local" },
                e => { e.MaxTextureSize = 1024; e.Local = true; e.BudgetBytes = 500L * 1024 * 1024; });
            yield return Case("odm-ktx2", new[] { "--texture-format", "Ktx2", "--ktx2-uastc", "--ktx2-zstd-level", "3", "--ktx2-threads", "2", "--max-texture-size", "1024", "--local" },
                e =>
                {
                    e.TextureFormat = QualityTextureFormat.Ktx2;
                    e.MaxTextureSize = 1024;
                    e.Local = true;
                    e.BudgetBytes = 500L * 1024 * 1024;

                });
            yield return Case("odm-3tz", new[] { "--texture-format", "Webp", "--local" },
                e =>
                {
                    e.Output3tz = true;
                    e.TextureFormat = QualityTextureFormat.Webp;
                    e.Local = true;
                    e.BudgetBytes = 500L * 1024 * 1024;

                }, out3tz: true);
            yield return Case("odm-lod-scale", new[] { "--lod-texture-scale", "0.5", "--max-texture-size", "512", "--local" },
                e =>
                {
                    e.LodTextureScale = 0.5;
                    e.MaxTextureSize = 512;
                    e.Local = true;
                    e.BudgetBytes = 500L * 1024 * 1024;

                });
        }
    }

    private static TestCaseData Case(string name, string[] args, Action<QualityExpectations> configure, bool out3tz = false)
    {
        var expected = new QualityExpectations { Output3tz = out3tz };
        configure(expected);
        var data = new TestCaseData(name, args, expected).SetName(name);
        return data;
    }

    [TestCaseSource(nameof(BrightonCases))]
    public void Brighton_geometry_contract(string name, string[] args, QualityExpectations expected)
    {
        using var dataset = QualityDataset.Brighton();
        CliQualityGateTests.RunValidateAndAssert(name, dataset, args, expected);
    }

    [TestCaseSource(nameof(OdmCases))]
    public void OdmTextured_geometry_contract(string name, string[] args, QualityExpectations expected)
    {
        if (!CliHarness.RunLarge)
        {
            Assert.Ignore($"heavy dataset: set {CliHarness.LargeRunEnv}=1 to run (nightly in CI)");
            return;
        }

        using var dataset = QualityDataset.OdmTextured();
        CliQualityGateTests.RunValidateAndAssert(name, dataset, args, expected);
    }
}

/// <summary>
/// The same invocation, twice: output must be reproducible (deterministic ordering and
/// serialization - catches ConcurrentBag ordering leaks and unstable ids/uuids).
/// </summary>
[TestFixture]
[Category("QualityGate")]
public class DeterminismTests
{
    [Test]
    public void SameOptions_produceReproducibleTileset()
    {
        using var dataset = QualityDataset.CubeTextured();

        var run1 = RunOnce(dataset);
        var run2 = RunOnce(dataset);

        run1.Tiles.ShouldBe(run2.Tiles, "tile content bytes differ between identical runs");
        run1.TilesetLength.ShouldBe(run2.TilesetLength, "tileset.json size changed between identical runs");
        // Map comparison (uri -> node): Shouldly reports precisely which tile diverges.
        TilesetMap(Path.Combine(run1.Dir, "tileset.json"))
            .ShouldBe(TilesetMap(Path.Combine(run2.Dir, "tileset.json")),
                "tileset.json is not reproducible between identical runs (volatile fields?)");
    }

    private static RunShape RunOnce(QualityDataset dataset)
    {
        var outBase = CliHarness.FreshOutputFolder("determinism-" + Guid.NewGuid().ToString("N")[..8]);
        var dir = Path.Combine(outBase, "out");

        var run = CliHarness.RunCli(new[] { "--local", dataset.InputObj, dir }, TimeSpan.FromMinutes(5));
        run.ExitCode.ShouldBe(0, run.Tail);

        // tileset.json is compared canonically (below): sibling tile order derives from the
        // pipeline's concurrent containers and is not semantically meaningful. Payload files
        // (b3dm and friends) must be byte-reproducible.
        var tiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith("tileset.json"))
            .Select(f => $"{Path.GetRelativePath(dir, f)}:{new FileInfo(f).Length}:" +
                         $"{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f)))}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var tilesetSize = new FileInfo(Path.Combine(dir, "tileset.json")).Length;
        return new RunShape(tiles, dir, tilesetSize);
    }

    /// <summary>
    /// URI-addressed view of the tileset: each tile contributes
    /// full/uri/path -> (node without children/asset/uuid) + sorted child uris.
    /// Sibling order is pipeline-concurrency noise (ConcurrentBag); everything addressed
    /// by tile uri - the contract consumers use - must still match exactly. Failures
    /// surface as a Shouldly map diff naming the diverging tile.
    /// </summary>
    private static Dictionary<string, string> TilesetMap(string path)
    {
        var token = Newtonsoft.Json.Linq.JToken.Parse(File.ReadAllText(path));
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var instanceSuffix = new Dictionary<string, int>(StringComparer.Ordinal);

        void Walk(Newtonsoft.Json.Linq.JToken node, string anc)
        {
            if (node is not Newtonsoft.Json.Linq.JObject obj) return;

            var canonical = new Newtonsoft.Json.Linq.JObject();
            foreach (var prop in obj.Properties())
            {
                if (prop.Name is "children" or "asset" or "uuid") continue;
                canonical[prop.Name] = prop.Value;
            }

            var childUris = new List<string>();
            if (obj["children"] is Newtonsoft.Json.Linq.JArray arr)
            {
                foreach (var child in arr)
                {
                    var uri = (string?)child["content"]?["uri"] ?? "(null)";
                    childUris.Add(uri);
                    Walk(child, $"{anc}/{uri}");
                }
                childUris.Sort(StringComparer.Ordinal);
            }

            canonical["__childUris"] = string.Join("|", childUris);

            var key = anc;
            if (instanceSuffix.TryGetValue(anc, out var n)) key = $"{anc}#{n}";
            instanceSuffix[anc] = instanceSuffix.GetValueOrDefault(anc, 0) + 1;
            map[key] = canonical.ToString(Newtonsoft.Json.Formatting.None);
        }

        var rootUri = (string?)token["root"]?["content"]?["uri"] ?? "root";
        Walk(token["root"]!, rootUri);
        return map;
    }

    private record RunShape(string[] Tiles, string Dir, long TilesetLength);
}
