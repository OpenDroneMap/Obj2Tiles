using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Obj2Tiles.Common;

namespace Obj2Tiles.Test.Quality;

/// <summary>
/// Result of running the Obj2Tiles CLI or the validator harness as a child process.
/// </summary>
public sealed record CliResult(int ExitCode, string Stdout, string Stderr)
{
    public string Tail => string.Join("\n",
        Stdout.Split('\n').Concat(Stderr.Split('\n')).Where(l => !string.IsNullOrWhiteSpace(l)).TakeLast(25));
}

/// <summary>
/// Shared plumbing for the CLI quality gate: locates the repository layout, runs the real
/// Obj2Tiles binary (not StagesFacade) and invokes the official 3D Tiles validator harness
/// (tools/validator/validate.cjs, library API + allowlist). Local-runnable: everything works
/// offline except dataset downloads, which honor the OBJ2TILES_TEST_DATA override.
/// </summary>
public static class CliHarness
{
    public const string TestDataSetEnv = "OBJ2TILES_TEST_DATA";
    public const string LargeRunEnv = "OBJ2TILES_RUN_LARGE";
    public const string SkipValidatorEnv = "OBJ2TILES_SKIP_VALIDATOR";
    public const string SkipNpmEnv = "OBJ2TILES_SKIP_NPM_INSTALL";

    private static readonly Lazy<string> LazyRepoRoot = new(FindRepoRoot);

    public static string RepoRoot => LazyRepoRoot.Value;
    public static string CliDll => typeof(Options).Assembly.Location;
    public static string TestDataRoot => Path.Combine(AppContext.BaseDirectory, "TestData");
    public static string TestOutputRoot => Path.Combine(AppContext.BaseDirectory, "TestOutput");
    public static string ValidatorDir => Path.Combine(RepoRoot, "tools", "validator");

    public static bool RunLarge => Environment.GetEnvironmentVariable(LargeRunEnv) == "1";

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Obj2Tiles.sln")))
            dir = dir.Parent;

        return dir?.FullName
                 ?? throw new InvalidOperationException(
                     $"Cannot locate Obj2Tiles.sln above {AppContext.BaseDirectory}");
    }

    /// <summary>
    /// Path to the directory bundling the native libktx for the current platform, or null.
    /// Plain (RID-less) builds do not copy it next to the executable, so the quality gate
    /// passes it explicitly via --ktx-path (see the packaging note in Obj2Tiles.csproj).
    /// </summary>
    public static string? KtxNativeDir
    {
        get
        {
            var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
            var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";

            foreach (var rid in new[] { $"{os}-{arch}", $"{os}-x64" })
            {
                var dir = Path.Combine(RepoRoot, "Obj2Tiles", "native", rid);
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir).Any())
                    return dir;
            }

            return null;
        }
    }

    public static CliResult RunCli(IEnumerable<string> args, TimeSpan? timeout = null, string? workingDir = null)
    {
        var full = new List<string> { "exec", CliDll };
        full.AddRange(args);

        var ktx = KtxNativeDir;
        if (ktx != null && !args.Contains("--ktx-path"))
            full.AddRange(new[] { "--ktx-path", ktx });

        return RunProcess("dotnet", full, timeout ?? TimeSpan.FromMinutes(5), workingDir);
    }

    // ---- official 3D Tiles validator (tools/validator harness) ------------------

    private static readonly Lazy<string?> NodeCheck = new(() =>
    {
        try
        {
            var r = RunProcess("node", new[] { "--version" }, TimeSpan.FromSeconds(10), ValidatorDir);
            return r.ExitCode == 0 ? null : $"node --version exited with {r.ExitCode}";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    });

    /// <summary>
    /// Null when the node-based validator harness can run; otherwise a human-readable
    /// reason tests should report when skipping (node missing / dependencies disabled).
    /// </summary>
    public static string? ValidatorUnavailableReason
    {
        get
        {
            if (Environment.GetEnvironmentVariable(SkipValidatorEnv) == "1")
                return $"{SkipValidatorEnv}=1 is set";

            var nodeIssue = NodeCheck.Value;
            if (nodeIssue != null)
                return $"Node.js is not usable here ({nodeIssue}). Install Node >= 18 to run the quality gate.";

            try
            {
                EnsureValidatorDependencies();
                return null;
            }
            catch (Exception ex)
            {
                return $"validator dependencies unavailable: {ex.Message}";
            }
        }
    }

    private static bool _npmEnsured;
    private static readonly object NpmLock = new();

    private static void EnsureValidatorDependencies()
    {
        lock (NpmLock)
        {
            if (_npmEnsured) return;

            var marker = Path.Combine(ValidatorDir, "node_modules", "3d-tiles-validator", "package.json");
            if (!File.Exists(marker))
            {
                if (Environment.GetEnvironmentVariable(SkipNpmEnv) == "1")
                    throw new InvalidOperationException(
                        $"3d-tiles-validator is not installed under {ValidatorDir} and {SkipNpmEnv}=1 " +
                        "prevents the automatic 'npm ci'. Run: npm ci --prefix tools/validator");

                var r = RunProcess("npm",
                    new[] { "ci", "--no-audit", "--no-fund", "--prefix", ValidatorDir },
                    TimeSpan.FromMinutes(10), RepoRoot);
                if (r.ExitCode != 0)
                    throw new InvalidOperationException($"'npm ci' failed ({r.ExitCode}): {r.Tail}");
            }

            _npmEnsured = true;
        }
    }

    /// <summary>
    /// Validates a produced tileset (loose folder tileset.json or .3tz package) with the
    /// official CesiumGS validator through the repository harness. The harness fails on any
    /// ERROR and on any WARNING not present in tools/validator/allowlist.json.
    /// </summary>
    public static CliResult RunValidator(string tilesetTarget, string reportPath)
    {
        return RunProcess("node",
            new[] { "validate.cjs", tilesetTarget, "--report", reportPath },
            TimeSpan.FromMinutes(5), ValidatorDir);
    }

    private static CliResult RunProcess(string program, IEnumerable<string> args, TimeSpan timeout, string? workingDir)
    {
        var psi = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDir ?? AppContext.BaseDirectory,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        // Read both pipes concurrently to avoid the classic full-buffer deadlock.
        var outTask = proc.StandardOutput.ReadToEndAsync();
        var errTask = proc.StandardError.ReadToEndAsync();

        if (!proc.WaitForExit((int)Math.Clamp(timeout.TotalMilliseconds, 1_000, int.MaxValue)))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* process may already be gone */ }
            throw new TimeoutException($"{program} {string.Join(' ', args)} exceeded {timeout}");
        }

        return new CliResult(proc.ExitCode, outTask.Result, errTask.Result);
    }

    /// <summary>
    /// Creates a fresh TestOutput folder for the given test, cleaning any previous run.
    /// </summary>
    public static string FreshOutputFolder(string testName)
    {
        var folder = Path.Combine(TestOutputRoot, testName);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        return folder;
    }
}

/// <summary>
/// Datasets for the quality gate. Small fixtures are committed in TestData/quality; larger
/// "real world" datasets resolve from the OBJ2TILES_TEST_DATA folder first (offline local
/// runs) and fall back to the DroneDB/test_data download cache (same URLs the existing
/// Mesh3Tests use; the data stays under the OS temp folder).
/// </summary>
public sealed class QualityDataset : IDisposable
{
    private const string TestDataRawBase = "https://github.com/DroneDB/test_data/raw/master/";

    public string InputObj { get; }
    public string DisplayName { get; }

    private readonly string? _ownedFolder;

    private QualityDataset(string inputObj, string displayName, string? ownedFolder = null)
    {
        InputObj = inputObj;
        DisplayName = displayName;
        _ownedFolder = ownedFolder;
    }

    public void Dispose()
    {
        if (_ownedFolder != null && Directory.Exists(_ownedFolder))
            Directory.Delete(_ownedFolder, true);
    }

    /// <summary>Small textured cube + vertex colors committed with the test project.</summary>
    public static QualityDataset CubeTextured() =>
        Open(Path.Combine(CliHarness.TestDataRoot, "quality", "cube-colors-textured", "cube-colors-textured.obj"),
            "cube-colors-textured");

    /// <summary>~4k vertex geometry-only beach mesh (real-world geometry, cheap).</summary>
    public static QualityDataset Brighton()
    {
        // The download lives in the shared temp cache and is intentionally kept for reuse.
        var (dir, _) = SharedFolder("brighton");
        var obj = Path.Combine(dir, "brighton_beach.obj");
        var mtl = Path.Combine(dir, "brighton_beach.obj.mtl");

        EnsureFile(obj, localCandidates("3d/brighton_beach.obj"), TestDataRawBase + "3d/brighton_beach.obj");
        EnsureFile(mtl, localCandidates("3d/brighton_beach.obj.mtl"), TestDataRawBase + "3d/brighton_beach.obj.mtl");

        return Open(obj, "brighton");
    }

    /// <summary>Full ODM photogrammetry model with textures (the Mesh3Tests dataset).</summary>
    public static QualityDataset OdmTextured()
    {
        var url = TestDataRawBase + "brighton/odm_texturing.zip";

        string archive;
        var localRoot = Environment.GetEnvironmentVariable(CliHarness.TestDataSetEnv);
        var localHit = localRoot == null
            ? null
            : new[] { Path.Combine(localRoot, "3d", "odm_texturing.zip"), Path.Combine(localRoot, "brighton", "odm_texturing.zip") }
                .FirstOrDefault(File.Exists);

        if (localHit != null)
        {
            archive = localHit;
        }
        else
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "obj2tiles_quality_gate", "odm");
            Directory.CreateDirectory(cacheDir);
            archive = Path.Combine(cacheDir, "odm_texturing.zip");
            if (!File.Exists(archive))
                HttpHelper.DownloadFileAsync(url, archive + ".tmp").Wait();
            if (!File.Exists(archive))
            {
                File.Move(archive + ".tmp", archive);
            }
        }

        var extract = Path.Combine(Path.GetTempPath(), "obj2tiles_quality_gate",
            "odm_extract_" + CommonUtils.RandomString(8));
        System.IO.Compression.ZipFile.ExtractToDirectory(archive, extract);

        return Open(Path.Combine(extract, "odm_textured_model_geo.obj"), "odm-textured", extract);
    }

    private static IEnumerable<string> localCandidates(string relative)
    {
        var root = Environment.GetEnvironmentVariable(CliHarness.TestDataSetEnv);
        if (root == null) yield break;

        yield return Path.Combine(root, relative);
        yield return Path.Combine(root, "test_data", relative);
    }

    private static void EnsureFile(string target, IEnumerable<string> localCandidates, string url)
    {
        if (File.Exists(target) && new FileInfo(target).Length > 0) return;

        foreach (var candidate in localCandidates)
        {
            if (candidate != target && File.Exists(candidate))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(candidate, target, true);
                return;
            }
        }

        CommonUtils.SmartDownloadFile(url, target);
    }

    private static (string Folder, bool Owned) SharedFolder(string name)
    {
        var folder = Path.Combine(Path.GetTempPath(), "obj2tiles_quality_gate", name);
        Directory.CreateDirectory(folder);
        return (folder, !Directory.EnumerateFiles(folder).Any());
    }

    private static QualityDataset Open(string inputObj, string display, string? owned = null)
    {
        if (!File.Exists(inputObj))
            throw new FileNotFoundException($"quality gate dataset input missing: {inputObj}", inputObj);

        return new QualityDataset(inputObj, display, owned);
    }
}
