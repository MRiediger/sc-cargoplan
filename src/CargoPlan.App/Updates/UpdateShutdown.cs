using System.Windows;
using System.Windows.Interop;

namespace CargoPlan.App.Updates;

/// <summary>
/// What comes between the download and <c>Application.Shutdown()</c> (T11.2, Freigabe E2). Once Update.exe has started, it
/// waits 60 s for this process and then kills it unflushed (util_common.rs 16, apply_windows_impl.rs 159): nothing may hold
/// the shutdown. No window cancels its Closing (none handles it); a modal dialog does hold it, so the apply waits for it.
/// </summary>
public static class UpdateShutdown
{
    /// <summary>
    /// Done once no modal dialog runs on this thread. A confirm or file dialog opened during the download runs its own
    /// message loop; Shutdown posts its work to the dispatcher (Application.cs CriticalShutdown) and would close the windows
    /// from inside that loop. Window.ShowDialog and Microsoft.Win32's CommonDialog both push a modal (CommonDialog.cs
    /// CriticalPushModal).
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.interop.componentdispatcher.isthreadmodal
    /// </summary>
    public static Task WhenNotModalAsync() =>
        WhenNotModalAsync(() => ComponentDispatcher.IsThreadModal, h => ComponentDispatcher.LeaveThreadModal += h, h => ComponentDispatcher.LeaveThreadModal -= h);

    internal static Task WhenNotModalAsync(Func<bool> isModal, Action<EventHandler> subscribe, Action<EventHandler> unsubscribe)
    {
        if (!isModal())
        {
            return Task.CompletedTask;
        }

        var done = new TaskCompletionSource();
        void Left(object? sender, EventArgs e)
        {
            // Dialogs can nest: only the last one leaving ends the wait.
            if (!isModal())
            {
                unsubscribe(Left);
                done.TrySetResult();
            }
        }

        subscribe(Left);
        return done.Task;
    }

    /// <summary>
    /// Closes every window but <paramref name="overlay"/> (editor, settings, theme sample) before the overlay goes: each by
    /// its own Close, as the player would, so its Closed runs as on the Exit button. The editor drops its draft rows as it
    /// always does when closed without Plan (D19); the plan itself is the overlay's and flushes with it.
    /// </summary>
    public static void CloseAllBut(IEnumerable<Window> windows, Window overlay)
    {
        // A copy: closing a window changes Application.Windows.
        foreach (var window in windows.Where(w => !ReferenceEquals(w, overlay)).ToList())
        {
            window.Close();
        }
    }
}
