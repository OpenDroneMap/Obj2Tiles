using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Obj2Tiles.Stages;

/// <summary>
/// Machine-readable progress for --progress: one compact JSON object per line on the given
/// writer (stdout by default), interleaved with the usual human-readable log lines. No human
/// log line starts with '{', so a reader picks the progress events by keeping only the lines
/// that do.
///
/// Line shapes:
///   {"stage":"decimation|splitting|root|tiling|packing","done":n,"total":m,"percent":p}
///   {"stage":"splitting","lod":i,"done":n,"total":m,"percent":p}   (done/total count the tiles of
///       LOD i; percent is the progress of the whole splitting stage, the mean of the LODs'
///       fractions: each LOD weighs the same. The first splitting line, without "lod", has
///       total = the number of LODs)
///   {"stage":"done","percent":100,"elapsed":seconds}
///   {"stage":"error","message":"..."}
///
/// percent is the progress of the current stage (0-100, one decimal). Writes are serialized,
/// so the parallel decimation, splitting and tiling workers never interleave a line, and
/// within a stage done never goes backwards.
/// </summary>
public sealed class ProgressReporter
{
    private readonly TextWriter _writer;
    private readonly object _lock = new();

    public ProgressReporter(TextWriter? writer = null)
    {
        _writer = writer ?? Console.Out;
    }

    /// <summary>Emits a stage-level event: done of total steps of the stage.</summary>
    public void Report(string stage, int done, int total)
    {
        Write(FormatStep(stage, done, total, Percent(done, total), null));
    }

    /// <summary>Starts a stage with a known number of steps (emits done = 0).</summary>
    public StageCounter Begin(string stage, int total) => new(this, stage, total);

    /// <summary>Starts a stage made of parts (LODs) whose own step counts are only known later.</summary>
    public PartsCounter BeginParts(string stage, int parts) => new(this, stage, parts);

    public void Done(TimeSpan elapsed)
    {
        Write(FormatDone(elapsed));
    }

    public void Error(string message)
    {
        Write(FormatError(message));
    }

    internal void Write(string line)
    {
        lock (_lock)
        {
            _writer.WriteLine(line);
            _writer.Flush();
        }
    }

    internal object SyncRoot => _lock;

    internal static double Percent(int done, int total) =>
        total <= 0 ? 100.0 : Math.Round(Math.Clamp(done * 100.0 / total, 0, 100), 1);

    public static string FormatStep(string stage, int done, int total, double percent, int? lod)
    {
        return Build(w =>
        {
            w.WriteString("stage", stage);
            if (lod.HasValue) w.WriteNumber("lod", lod.Value);
            w.WriteNumber("done", done);
            w.WriteNumber("total", total);
            w.WriteNumber("percent", Math.Round(percent, 1));
        });
    }

    public static string FormatDone(TimeSpan elapsed)
    {
        return Build(w =>
        {
            w.WriteString("stage", "done");
            w.WriteNumber("percent", 100);
            w.WriteNumber("elapsed", Math.Round(elapsed.TotalSeconds, 3));
        });
    }

    public static string FormatError(string message)
    {
        return Build(w =>
        {
            w.WriteString("stage", "error");
            w.WriteString("message", message);
        });
    }

    private static string Build(Action<Utf8JsonWriter> body)
    {
        using var ms = new MemoryStream();
        // Relaxed escaping keeps paths and accented messages readable; the output is never HTML.
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions
               {
                   Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
               }))
        {
            w.WriteStartObject();
            body(w);
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>Thread-safe step counter of one stage.</summary>
    public sealed class StageCounter
    {
        private readonly ProgressReporter _reporter;
        private readonly string _stage;
        private int _done;

        internal StageCounter(ProgressReporter reporter, string stage, int total)
        {
            _reporter = reporter;
            _stage = stage;
            Total = total;
            reporter.Report(stage, 0, total);
        }

        public int Total { get; }

        public void Advance()
        {
            // Increment and write under the same lock so the emitted done values are monotonic.
            lock (_reporter.SyncRoot)
            {
                _done = Math.Min(_done + 1, Total);
                _reporter.Report(_stage, _done, Total);
            }
        }
    }

    /// <summary>
    /// Thread-safe counter of a stage made of parallel parts (the LODs of the splitting stage):
    /// each part learns its own step count when it gets there, and the stage percent is the
    /// mean of the parts' fractions (a part with no known count yet weighs 0).
    /// </summary>
    public sealed class PartsCounter
    {
        private readonly ProgressReporter _reporter;
        private readonly string _stage;
        private readonly int[] _done;
        private readonly int[] _total;

        internal PartsCounter(ProgressReporter reporter, string stage, int parts)
        {
            _reporter = reporter;
            _stage = stage;
            _done = new int[parts];
            _total = Enumerable.Repeat(-1, parts).ToArray();
            reporter.Report(stage, 0, parts);
        }

        public void SetTotal(int part, int total)
        {
            lock (_reporter.SyncRoot)
            {
                _total[part] = Math.Max(0, total);
                Emit(part);
            }
        }

        public void Advance(int part)
        {
            lock (_reporter.SyncRoot)
            {
                if (_total[part] >= 0)
                    _done[part] = Math.Min(_done[part] + 1, _total[part]);
                else
                    _done[part]++;
                Emit(part);
            }
        }

        private void Emit(int part)
        {
            var sum = 0.0;
            for (var i = 0; i < _done.Length; i++)
            {
                if (_total[i] < 0) continue;
                sum += _total[i] == 0 ? 1.0 : (double)_done[i] / _total[i];
            }

            var percent = _done.Length == 0 ? 100.0 : Math.Round(sum * 100.0 / _done.Length, 1);
            _reporter.Write(FormatStep(_stage, _done[part], Math.Max(0, _total[part]), percent, part));
        }
    }
}
