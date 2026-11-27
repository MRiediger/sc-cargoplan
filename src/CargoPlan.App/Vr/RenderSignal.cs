using System.Windows.Threading;

namespace CargoPlan.App.Vr;

/// <summary>
/// The dirty signal of the VR feed (T14.2, K1): after every dispatcher operation of render work, which is where WPF lays out
/// and renders (priority Render, and Input, to which WPF promotes a starving render), the UI thread calls
/// <paramref name="signal"/>. At rest WPF posts no such work, so the feed takes no picture. Not CompositionTarget.Rendering:
/// with a handler it renders every frame (S14.0). The app's own timers run at Background priority and don't count.
/// Which priority WPF renders at is WPF's internal; RenderSignalTests guard it (brush, camera, animation, rest).
/// </summary>
/// <param name="signal">Runs on the UI thread with every such operation; must be cheap (VrFeed.MarkDirty).</param>
internal sealed class RenderSignal(Dispatcher dispatcher, Action signal)
{
    private bool _attached;

    /// <summary>UI thread. With VR on only, so VR off costs nothing (invariant 3).</summary>
    public void Attach()
    {
        if (!_attached)
        {
            _attached = true;
            // "This event is raised by the dispatcher thread after the operation has completed."
            // Source: https://learn.microsoft.com/dotnet/api/system.windows.threading.dispatcherhooks.operationcompleted
            dispatcher.Hooks.OperationCompleted += OnCompleted;
        }
    }

    public void Detach()
    {
        if (_attached)
        {
            _attached = false;
            dispatcher.Hooks.OperationCompleted -= OnCompleted;
        }
    }

    // Source: https://learn.microsoft.com/dotnet/api/system.windows.threading.dispatcheroperation.priority
    private void OnCompleted(object? sender, DispatcherHookEventArgs e)
    {
        if (e.Operation.Priority is DispatcherPriority.Render or DispatcherPriority.Input)
        {
            signal();
        }
    }
}
