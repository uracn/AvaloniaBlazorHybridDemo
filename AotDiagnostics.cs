using System.Diagnostics;
using System.Text;

namespace AvaloniaBlazorHybridDemo;

/// <summary>
/// Lightweight startup diagnostics. Enabled only when URA_AOT_DEBUG=1 so the
/// shipped binary does not write logs. Captures Trace output (Avalonia's
/// LogToTrace sink), unhandled exceptions and unobserved task exceptions.
/// </summary>
internal static class AotDiagnostics
{
    private static readonly object Gate = new();
    private static StreamWriter? _writer;

    public static bool Enabled => Environment.GetEnvironmentVariable("URA_AOT_DEBUG") == "1";

    public static void Init()
    {
        if (!Enabled)
            return;

        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "aot-debug.log");
            // One writer owns the file; every Log() call goes through it. A second
            // StreamWriter would lock the file and silently swallow all later logs.
            _writer = new StreamWriter(logPath, append: false, Encoding.UTF8) { AutoFlush = true };
            Log($"START BaseDirectory={AppContext.BaseDirectory}");
        }
        catch
        {
            _writer = null;
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log($"UNHANDLED: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
            Log($"UNOBSERVED: {e.Exception}");

        try
        {
            Trace.Listeners.Add(new TextWriterTraceListener(_writer));
            Trace.AutoFlush = true;
        }
        catch
        {
            // tracing is best-effort
        }
    }

    public static void Log(string message)
    {
        var writer = _writer;
        if (!Enabled || writer is null)
            return;

        lock (Gate)
        {
            try
            {
                writer.WriteLine($"[{DateTime.Now:O}] {message}");
                writer.Flush();
            }
            catch
            {
                // ignore
            }
        }
    }
}
