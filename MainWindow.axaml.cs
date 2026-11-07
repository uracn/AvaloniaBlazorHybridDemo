using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaBlazorHybridDemo;

public partial class MainWindow : Window
{
    private int _nativeClicks;

    public MainWindow()
    {
        InitializeComponent();
    }

    // Native Avalonia event -> Razor component, via a plain .NET event bridge.
    private void OnSendToBlazor(object? sender, RoutedEventArgs e)
    {
        _nativeClicks++;
        NativeBridge.Publish($"来自 Avalonia 原生按钮的第 {_nativeClicks} 次消息");
    }
}
