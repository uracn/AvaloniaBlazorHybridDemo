using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using AvaloniaBlazorHybridDemo.Blazor;

namespace AvaloniaBlazorHybridDemo;

/// <summary>
/// Hosts Avalonia 12's official NativeWebView and wires it to Blazor's WebViewManager.
///
/// Why the official control: its Windows backend uses GeneratedComInterface /
/// StrategyBasedComWrappers, so it works under NativeAOT. The legacy
/// Microsoft.Web.WebView2.Core [ComImport] stack cannot (IL3052).
///
/// Static content is served by WebView2's SetVirtualHostNameToFolderMapping. That one
/// method is not exposed by Avalonia's abstraction, so we invoke it directly through
/// the ICoreWebView2_3 vtable obtained from IWindowsWebView2PlatformHandle.
/// </summary>
public sealed class BlazorWebViewHost : ContentControl
{
    private const string HostName = "app.local";
    private const int SetVirtualHostNameToFolderMappingSlot = 71;
    private const int HostResourceAccessKindAllow = 1;

    // ICoreWebView2_3
    private static readonly Guid ICoreWebView2_3Iid =
        new("A0D6DF20-3B92-416D-AA0C-437A9C727857");

    private readonly NativeWebView _webView = new();
    private AppWebViewManager? _manager;
    private bool _initialized;

    public BlazorWebViewHost()
    {
        Content = _webView;
        _webView.EnvironmentRequested += OnEnvironmentRequested;
        _webView.AdapterCreated += OnAdapterCreated;
        _webView.WebMessageReceived += OnWebMessageReceived;
    }

    private void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
        e.EnableDevTools = true;
        if (e is WindowsWebView2EnvironmentRequestedEventArgs win)
        {
            // Lets diagnostics pass --remote-debugging-port without changing the app.
            var extra = Environment.GetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS");
            if (!string.IsNullOrWhiteSpace(extra))
                win.AdditionalBrowserArguments = extra;
        }
    }

    private void OnAdapterCreated(object? sender, WebViewAdapterEventArgs e)
    {
        if (_initialized)
            return;
        _initialized = true;

        try
        {
            InitializeBlazor();
        }
        catch (Exception ex)
        {
            AotDiagnostics.Log($"BlazorWebViewHost FAILED: {ex}");
        }
    }

    private void InitializeBlazor()
    {
        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (!Directory.Exists(wwwroot))
        {
            AotDiagnostics.Log($"wwwroot missing: {wwwroot}");
            return;
        }

        if (_webView.TryGetPlatformHandle() is not IWindowsWebView2PlatformHandle handle)
        {
            AotDiagnostics.Log("Windows WebView2 platform handle unavailable");
            return;
        }

        MapVirtualHost(handle.CoreWebView2, HostName, wwwroot);
        AotDiagnostics.Log($"VirtualHost {HostName} -> {wwwroot}");

        var services = App.Services;
        var manager = new AppWebViewManager(
            services,
            App.BlazorDispatcher,
            new PhysicalFileProvider(wwwroot),
            services.GetRequiredService<JSComponentConfigurationStore>(),
            _webView);
        _manager = manager;

        _ = manager.AddRootComponentAsync(
                typeof(Components.AppWeb), "#app", ParameterView.Empty)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    AotDiagnostics.Log("AddRootComponentAsync FAILED: " + t.Exception);
                else
                    AotDiagnostics.Log("AddRootComponentAsync completed");
            }, TaskScheduler.Default);
        manager.Navigate("/index.html");
        AotDiagnostics.Log("Blazor root component registered, navigating to /index.html");
    }

    private void OnWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        AotDiagnostics.Log("JS->NET: " + Truncate(e.Body));
        if (string.IsNullOrEmpty(e.Body))
            return;
        _manager?.OnWebMessage(AppWebViewManager.AppBaseUri, e.Body);
    }

    internal static string Truncate(string? value)
        => string.IsNullOrEmpty(value) ? "<empty>" : value[..Math.Min(100, value.Length)];

    /// <summary>
    /// ICoreWebView2_3::SetVirtualHostNameToFolderMapping(LPCWSTR, LPCWSTR, enum) -> HRESULT.
    /// Vtable layout: IUnknown (3) + ICoreWebView2 (58) + ICoreWebView2_2 (7)
    /// + ICoreWebView2_3 [TrySuspend(68), Resume(69), get_IsSuspended(70),
    /// SetVirtualHostNameToFolderMapping(71), Clear(72)].
    /// COM interfaces are immutable once published, so the slot is stable.
    /// </summary>
    private static unsafe void MapVirtualHost(IntPtr coreWebView2, string hostName, string folder)
    {
        if (coreWebView2 == IntPtr.Zero)
            throw new InvalidOperationException("CoreWebView2 handle is null.");

        var iid = ICoreWebView2_3Iid;
        var hr = Marshal.QueryInterface(coreWebView2, in iid, out var webView3);
        Marshal.ThrowExceptionForHR(hr);
        try
        {
            var vtable = *(nint**)webView3;
            var setMapping = (delegate* unmanaged[Stdcall]<nint, char*, char*, int, int>)
                vtable[SetVirtualHostNameToFolderMappingSlot];

            fixed (char* host = hostName)
            fixed (char* path = folder)
            {
                Marshal.ThrowExceptionForHR(setMapping(webView3, host, path, HostResourceAccessKindAllow));
            }
        }
        finally
        {
            Marshal.Release(webView3);
        }
    }
}
