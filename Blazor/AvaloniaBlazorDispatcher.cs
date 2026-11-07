using BlazorDispatcher = Microsoft.AspNetCore.Components.Dispatcher;
using AvaloniaDispatcher = Avalonia.Threading.Dispatcher;

namespace AvaloniaBlazorHybridDemo.Blazor;

/// <summary>
/// Marshals Blazor renderer work onto Avalonia's UI thread. Blazor requires that
/// all component rendering happens on a single synchronization context.
/// </summary>
internal sealed class AvaloniaBlazorDispatcher : BlazorDispatcher
{
    private readonly AvaloniaDispatcher _ui = AvaloniaDispatcher.UIThread;

    public override bool CheckAccess() => _ui.CheckAccess();

    public override async Task InvokeAsync(Action workItem)
    {
        if (_ui.CheckAccess())
            workItem();
        else
            await _ui.InvokeAsync(workItem).GetTask();
    }

    public override async Task InvokeAsync(Func<Task> workItem)
    {
        if (_ui.CheckAccess())
            await workItem();
        else
            await _ui.InvokeAsync(workItem);
    }

    public override async Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
    {
        if (_ui.CheckAccess())
            return workItem();
        return await _ui.InvokeAsync(workItem);
    }

    public override async Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
    {
        if (_ui.CheckAccess())
            return await workItem();
        return await _ui.InvokeAsync(workItem);
    }
}
