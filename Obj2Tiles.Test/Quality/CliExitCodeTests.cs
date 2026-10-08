using System.IO;
using NUnit.Framework;
using Shouldly;

namespace Obj2Tiles.Test.Quality;

/// <summary>Process exit codes of informational invocations (batch/CI callers rely on them).</summary>
[TestFixture]
[Category("QualityGate")]
public class CliExitCodeTests
{
    [TestCase("--help")]
    [TestCase("--version")]
    public void InformationalFlag_exitsWithZero(string flag)
    {
        var run = CliHarness.RunCli(new[] { flag }, workingDir: Path.GetTempPath());

        run.ExitCode.ShouldBe(0, run.Tail);
    }
}
