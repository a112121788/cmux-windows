using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Cmux.Core.Terminal;

namespace Cmux.Smoke;

internal static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AllocConsole();

    private static StreamWriter _log = StreamWriter.Null;
    private static int _failures;
    private static int _outputEvents;
    private static int _outputBytes;

    private static void Check(string label, bool ok, string? detail = null)
    {
        var line = ok ? $"  PASS  {label}" : $"  FAIL  {label}{(detail is null ? "" : $"  -- {detail}")}";
        _log.WriteLine(line);
        if (!ok) _failures++;
    }

    private static void Log(string line) => _log.WriteLine(line);

    [STAThread]
    private static async Task<int> Main()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "cmux-smoke.log");
        _log = new StreamWriter(logPath, append: false) { AutoFlush = true };
        Log($"=== Cmux ConPTY smoke (log: {logPath}) ===");
        Log($"  [setup] FreeConsole result: {FreeConsole()}");

        try
        {
            await TestEnvironmentInjection();
        }
        finally
        {
            AllocConsole();
        }

        Log("");
        Log(_failures == 0 ? "ALL CHECKS PASSED" : $"{_failures} CHECK(S) FAILED");
        _log.Dispose();
        return _failures == 0 ? 0 : 1;
    }

    private static async Task TestEnvironmentInjection()
    {
        Log("\n[1] Environment injection (with direct pipe read)");
        using var session = new TerminalSession("smoke-env", 120, 30);
        session.OutputReceived += () => Interlocked.Increment(ref _outputEvents);
        session.RawOutputReceived += bytes => Interlocked.Add(ref _outputBytes, bytes.Length);
        session.ProcessExited += () => Log("    [env] ProcessExited");
        session.Start();
        var pid = session.ProcessId;
        Log($"    [env] ProcessId={pid}");
        if (pid != null)
        {
            try { var p = Process.GetProcessById(pid.Value); Log($"    [env] process alive, hasExited={p.HasExited}"); }
            catch (Exception ex) { Log($"    [env] process lookup failed: {ex.Message}"); }
        }
        await Task.Delay(3000);
        if (pid != null)
        {
            try { var p = Process.GetProcessById(pid.Value); Log($"    [env] process after 3s, hasExited={p.HasExited}"); }
            catch (Exception ex) { Log($"    [env] process after 3s lookup failed: {ex.Message}"); }
        }
        Log($"    [env] events={Interlocked.CompareExchange(ref _outputEvents, 0, 0)} bytes={Interlocked.CompareExchange(ref _outputBytes, 0, 0)}");
        // Read the read pipe directly via the handle the producer task
        // also uses. If we see data here but events=0, the channel/parser
        // path is the problem; if we see no data here, ConPTY itself is
        // not delivering.
        var consoleField = typeof(TerminalSession).GetField("_console", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var console = (PseudoConsole?)consoleField.GetValue(session);
        if (console != null)
        {
            Log("    [env] direct pipe read for 2s...");
            using var fs = new FileStream(console.ReadPipe, FileAccess.Read, bufferSize: 4096, isAsync: false);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var buf = new byte[4096];
            try
            {
                int n = await fs.ReadAsync(buf, 0, buf.Length, cts.Token);
                Log($"    [env] direct read returned {n} bytes: {EscapeBytes(buf, n)}");
            }
            catch (Exception ex)
            {
                Log($"    [env] direct read exception: {ex.Message}");
            }
        }
    }

    private static string EscapeBytes(byte[] buf, int n)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Math.Min(n, 200); i++)
        {
            byte b = buf[i];
            if (b >= 0x20 && b < 0x7F) sb.Append((char)b);
            else if (b == 0x1B) sb.Append("\\e");
            else if (b == 0x0A) sb.Append("\\n");
            else if (b == 0x0D) sb.Append("\\r");
            else sb.Append($"\\x{b:X2}");
        }
        return sb.ToString();
    }
}