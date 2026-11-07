using System.Text;
using Avalonia.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.FileProviders;
using BlazorDispatcher = Microsoft.AspNetCore.Components.Dispatcher;
using AvaloniaDispatcher = Avalonia.Threading.Dispatcher;

namespace AvaloniaBlazorHybridDemo.Blazor;

/// <summary>
/// AOT-safe WebViewManager implementation backed by Avalonia 12's NativeWebView.
/// Razor components run in this process; the native WebView only renders the DOM.
/// Static files come from an explicit PhysicalFileProvider (no Assembly.Location),
/// and IPC messages travel over Avalonia's invokeCSharpAction / InvokeScript bridge.
/// </summary>
internal sealed class AppWebViewManager : WebViewManager
{
    public static readonly Uri AppBaseUri = new("https://app.local/");

    private readonly NativeWebView _webView;
    private int _sent;

    public AppWebViewManager(
        IServiceProvider provider,
        BlazorDispatcher dispatcher,
        IFileProvider fileProvider,
        JSComponentConfigurationStore jsComponents,
        NativeWebView webView)
        : base(provider, dispatcher, AppBaseUri, fileProvider, jsComponents, "index.html")
    {
        _webView = webView;
        AotDiagnostics.Log("AppWebViewManager created");
    }

    protected override void NavigateCore(Uri absoluteUri)
        => AvaloniaDispatcher.UIThread.Post(() => _webView.Navigate(absoluteUri));

    protected override void SendMessage(string message)
    {
        // Avoid System.Text.Json here on purpose: reflection-based serialization is
        // fragile under NativeAOT, while a JS string literal is trivial to build.
        var script = "window.__blazorReceive(" + ToJsStringLiteral(message) + ")";
        if (++_sent <= 8)
            AotDiagnostics.Log("NET->JS #" + _sent + " (len=" + message.Length + "): "
                               + message[..Math.Min(2000, message.Length)]);

        void Invoke()
        {
            try
            {
                _ = _webView.InvokeScript(script);
            }
            catch (Exception ex)
            {
                AotDiagnostics.Log("InvokeScript FAILED: " + ex);
            }
        }

        if (AvaloniaDispatcher.UIThread.CheckAccess())
            Invoke();
        else
            AvaloniaDispatcher.UIThread.Post(Invoke);
    }

    /// <summary>Forwards a message coming from blazor.webview.js into WebViewManager.</summary>
    public void OnWebMessage(Uri sourceUri, string message)
    {
        try
        {
            MessageReceived(sourceUri, message);
        }
        catch (Exception ex)
        {
            AotDiagnostics.Log("MessageReceived FAILED: " + ex);
        }
    }

    private static string ToJsStringLiteral(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '<': sb.Append("\\u003C"); break;   // avoid closing </script>
                case '>': sb.Append("\\u003E"); break;
                case '&': sb.Append("\\u0026"); break;
                case '\'': sb.Append("\\u0027"); break;
                default:
                    if (c < 0x20 || c > 0x7E)
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
