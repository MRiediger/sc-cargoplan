using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CargoPlan.App.Win32;

namespace CargoPlan.App;

/// <summary>
/// A scroll bar's thumb or track button lets go when the left button is up though no WM_LBUTTONUP came (T13.3a, D11
/// addendum): over the game the release can go to the game, and the thumb would drag on, the track button repeat on.
/// While one of them holds the capture, each frame reads the physical button (as the ship view's drags, D45); up after
/// down releases the capture, which ends both: the thumb cancels its drag when it loses the capture, the repeat button
/// stops its timer (dotnet/wpf Thumb.cs OnLostMouseCapture → CancelDrag; RepeatButton.cs OnLostMouseCapture → StopTimer).
/// </summary>
internal sealed class ScrollCaptureGuard
{
    private UIElement? _holder;
    private ButtonWatch _watch = new();

    /// <summary>Watches every thumb and track button in <paramref name="window"/>.</summary>
    public static void Attach(Window window)
    {
        var guard = new ScrollCaptureGuard();
        // Handled ones too: the thumb and the buttons handle the press that takes the capture.
        window.AddHandler(Mouse.GotMouseCaptureEvent, new MouseEventHandler(guard.OnGotCapture), handledEventsToo: true);
        window.AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler(guard.OnLostCapture), handledEventsToo: true);
    }

    private void OnGotCapture(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource is not (Thumb or RepeatButton))
        {
            return;
        }

        var holder = (UIElement)e.OriginalSource;

        _holder = holder;
        _watch = new ButtonWatch();
        _watch.ShouldRelease(NativeMethods.IsMouseButtonDown(MouseButton.Left));

        // -= first: never two handlers on the static event.
        CompositionTarget.Rendering -= OnRendering;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, _holder))
        {
            Stop();
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_holder is not { } holder || !ReferenceEquals(Mouse.Captured, holder))
        {
            Stop();
            return;
        }

        if (_watch.ShouldRelease(NativeMethods.IsMouseButtonDown(MouseButton.Left)))
        {
            AppLog.Write("Input", $"{holder.GetType().Name} let go: the button is up, no WM_LBUTTONUP came");
            Stop();
            holder.ReleaseMouseCapture();
        }
    }

    private void Stop()
    {
        CompositionTarget.Rendering -= OnRendering;
        _holder = null;
    }

    /// <summary>Up counts only after a down: the call fails towards an elevated window and then always says up (D45).</summary>
    internal sealed class ButtonWatch
    {
        private bool _sawDown;

        public bool ShouldRelease(bool buttonDown)
        {
            _sawDown |= buttonDown;
            return _sawDown && !buttonDown;
        }
    }
}
