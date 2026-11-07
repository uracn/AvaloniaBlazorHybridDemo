using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaBlazorHybridDemo.Components;
using AvaloniaBlazorWebView;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaBlazorHybridDemo;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void RegisterServices()
    {
        base.RegisterServices();

        // Blazor Hybrid: Razor components run in this process and are rendered
        // into a native WebView. No web server, no WASM, no SignalR circuit.
        AvaloniaBlazorWebViewBuilder.Initialize(
            webConfig =>
            {
                webConfig.AreDevToolEnabled = true;
                webConfig.AreDefaultContextMenusEnabled = true;
            },
            setting =>
            {
                setting.ComponentType = typeof(AppWeb);
                setting.Selector = "#app";
                setting.WWWRoot = "wwwroot";
                setting.ResourceAssembly = typeof(AppWeb).Assembly;
            },
            services =>
            {
                // Any .NET service registered here can be @inject-ed in Razor,
                // which is what makes this "Hybrid" rather than a hosted web app.
                services.AddSingleton<NativeHostInfo>();
            });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
