namespace AvaloniaBlazorHybridDemo;

/// <summary>
/// Minimal bridge for native Avalonia UI -> Razor component communication.
/// No HTTP, no JS interop required: both sides live in the same CLR process.
/// </summary>
public static class NativeBridge
{
    public static event Action<string>? MessageReceived;

    public static string LastMessage { get; private set; } = "（还没有消息，点击窗口右上角的原生按钮试试）";

    public static void Publish(string message)
    {
        LastMessage = message;
        MessageReceived?.Invoke(message);
    }
}
