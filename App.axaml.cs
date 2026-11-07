using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaBlazorHybridDemo.Blazor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using BlazorDispatcher = Microsoft.AspNetCore.Components.Dispatcher;

namespace AvaloniaBlazorHybridDemo;

public partial class App : Application
{
    /// <summary>DI container shared by the native shell and the Razor components.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Blazor's renderer dispatcher, bound to Avalonia's UI thread.</summary>
    public static BlazorDispatcher BlazorDispatcher { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void RegisterServices()
    {
        base.RegisterServices();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorWebView();
        services.TryAddSingleton<JSComponentConfigurationStore>();

        // Any .NET service registered here can be @inject-ed in Razor - that is what
        // makes this a real Hybrid app rather than a hosted web page.
        services.AddSingleton<NativeHostInfo>();

        Services = services.BuildServiceProvider();
        BlazorDispatcher = new AvaloniaBlazorDispatcher();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
