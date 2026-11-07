using Avalonia;
using Avalonia.WebView.Desktop;

namespace AvaloniaBlazorHybridDemo;

internal class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace()
            // Registers the native WebView handler (WebView2 on Windows,
            // WKWebView on macOS, WebKitGTK on Linux).
            .UseDesktopWebView();
}
