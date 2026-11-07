using Avalonia;
using AvaloniaBlazorHybridDemo.Blazor;

namespace AvaloniaBlazorHybridDemo;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AotDiagnostics.Init();

        // Must run before the first Blazor IPC message: the shared
        // JsonSerializerOptions becomes immutable once it has been used.
        IpcJsonWorkaround.Apply();

        try
        {
            AotDiagnostics.Log("Starting Avalonia");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            AotDiagnostics.Log("Avalonia lifetime exited normally");
        }
        catch (Exception ex)
        {
            AotDiagnostics.Log($"STARTUP FAILED: {ex}");
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
