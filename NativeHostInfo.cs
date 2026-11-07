using System.Runtime.InteropServices;
using Avalonia;

namespace AvaloniaBlazorHybridDemo;

/// <summary>
/// A plain .NET service registered in the Blazor DI container. Razor components
/// call into it directly - this code runs in the desktop process, not in a browser.
/// </summary>
public sealed class NativeHostInfo
{
    public string Os => RuntimeInformation.OSDescription;

    public string Framework => RuntimeInformation.FrameworkDescription;

    public string Architecture => RuntimeInformation.ProcessArchitecture.ToString();

    public int ProcessId => Environment.ProcessId;

    public string Machine => Environment.MachineName;

    public string AvaloniaVersion =>
        typeof(Application).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>Major.minor of the Avalonia runtime actually loaded, e.g. "12.1".</summary>
    public string AvaloniaMajorVersion =>
        typeof(Application).Assembly.GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}"
            : "unknown";

    public string StartedAt { get; } = DateTime.Now.ToString("HH:mm:ss");
}
