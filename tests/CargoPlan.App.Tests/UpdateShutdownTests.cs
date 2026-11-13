using System.Windows;
using CargoPlan.App.Settings;
using CargoPlan.App.Updates;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// E2 (Freigabe T11.2): nothing may hold the shutdown after Update.exe started, or its 60 s run out and it kills the app
/// unflushed. Application.Shutdown() itself can't run here (the test process has no Application on purpose, see
/// <see cref="ToolWindowThemerTests"/>); Gate 1 runs it. These cover what comes before it.
/// </summary>
public class UpdateShutdownTests
{
    // Fluent's resources load through pack://, which Application's static constructor sets up (ToolWindowThemerTests).
    static UpdateShutdownTests() => _ = Application.Current;

    [Fact]
    public void The_tool_windows_close_with_an_editor_open_and_a_draft_in_it()
    {
        Sta.Run(() =>
        {
            var overlay = new OverlayViewModel(FrozenShips.Load("hull_a"));
            var owner = Offscreen();
            owner.Show();
            var editorModel = new MissionEditorViewModel(overlay);
            editorModel.AddRowCommand.Execute(null);
            var editor = new MissionEditorWindow(editorModel, overlay, ToolWindowTheme.Dark, owner);
            editor.Show();
            List<string> closed = [];
            editor.Closed += (_, _) => closed.Add("editor");
            owner.Closed += (_, _) => closed.Add("overlay");

            UpdateShutdown.CloseAllBut([owner, editor], owner);

            closed.Should().Equal("editor");
            owner.Close();
            closed.Should().Equal("editor", "overlay");
        });
    }

    [Fact]
    public async Task Without_a_modal_dialog_the_apply_goes_on_at_once()
    {
        var modal = new FakeModal(isModal: false);

        await UpdateShutdown.WhenNotModalAsync(() => modal.IsModal, modal.Add, modal.Remove).WaitAsync(TimeSpan.FromSeconds(5));

        modal.Handlers.Should().Be(0);
    }

    [Fact]
    public async Task A_modal_dialog_holds_the_apply_until_it_closes()
    {
        // A confirm or file dialog opened during the download runs its own loop; a shutdown inside it would close the
        // windows under the dialog's feet (Shutdown posts its work to the dispatcher, Application.cs CriticalShutdown).
        var modal = new FakeModal(isModal: true);

        var waiting = UpdateShutdown.WhenNotModalAsync(() => modal.IsModal, modal.Add, modal.Remove);

        waiting.IsCompleted.Should().BeFalse();
        modal.Leave();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        modal.Handlers.Should().Be(0);
    }

    private static Window Offscreen() => new()
    {
        Width = 50,
        Height = 50,
        Left = -10000,
        Top = -10000,
        ShowActivated = false,
        ShowInTaskbar = false,
        WindowStyle = WindowStyle.None,
    };

    /// <summary>ComponentDispatcher's IsThreadModal and LeaveThreadModal, as a test can drive them.</summary>
    private sealed class FakeModal(bool isModal)
    {
        private EventHandler? _leave;

        public bool IsModal { get; private set; } = isModal;

        public int Handlers => _leave?.GetInvocationList().Length ?? 0;

        public void Add(EventHandler handler) => _leave += handler;

        public void Remove(EventHandler handler) => _leave -= handler;

        public void Leave()
        {
            IsModal = false;
            _leave?.Invoke(null, EventArgs.Empty);
        }
    }
}
