using CargoPlan.App.Hotkeys;
using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App.Tests;

public sealed class HotkeysViewModelTests
{
    private const uint VK_L = 0x4C;
    private const uint VK_H = 0x48;
    private const uint VK_F12 = 0x7B;
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    private readonly List<string> _calls = [];
    private readonly List<(HotkeyAction Action, Hotkey? Hotkey)> _set = [];

    // What the service answers to the next set: null = registered, text = refused.
    private string? _refusal;

    private HotkeysViewModel Create(HotkeySettings? settings = null) => new(
        settings,
        (action, hotkey) =>
        {
            _calls.Add("set");
            _set.Add((action, hotkey));
            return _refusal;
        },
        () => _calls.Add("suspend"),
        () => _calls.Add("resume"));

    private static Hotkey Key(string text)
    {
        HotkeyParser.TryParse(text, out var hotkey, out _).Should().BeTrue();
        return hotkey!;
    }

    [Fact]
    public void Opening_shows_the_hotkeys_of_the_file_and_changes_nothing()
    {
        var viewModel = Create(new HotkeySettings("Ctrl+Alt+L", null));

        viewModel.Lock.Hotkey.Should().Be(Key("Ctrl+Alt+L"));
        viewModel.Lock.Display.Should().Be("Ctrl+Alt+L");
        viewModel.Overlay.Hotkey.Should().BeNull();
        viewModel.Overlay.Display.Should().Be(HotkeyEditor.NotSetText);
        _calls.Should().BeEmpty();
    }

    [Fact]
    public void An_invalid_hotkey_in_the_file_shows_why()
    {
        var viewModel = Create(new HotkeySettings("Shift+L", null));

        viewModel.Lock.Hotkey.Should().BeNull();
        viewModel.Lock.Problem.Should().Contain("Shift+L").And.Contain("needs Ctrl, Alt or Win");
    }

    [Fact]
    public void Recording_suspends_our_hotkeys_so_their_keys_arrive_here()
    {
        var viewModel = Create();

        viewModel.Lock.RecordCommand.Execute(null);

        viewModel.Lock.IsRecording.Should().BeTrue();
        viewModel.Lock.Display.Should().Be(HotkeyEditor.RecordingText);
        _calls.Should().Equal("suspend");
    }

    [Fact]
    public void A_valid_combination_resumes_then_sets_and_shows_it()
    {
        var viewModel = Create();
        viewModel.Lock.RecordCommand.Execute(null);

        viewModel.Lock.Capture(CtrlAlt, VK_L);

        _calls.Should().Equal("suspend", "resume", "set");
        _set.Should().Equal((HotkeyAction.ToggleLock, Key("Ctrl+Alt+L")));
        viewModel.Lock.IsRecording.Should().BeFalse();
        viewModel.Lock.Hotkey.Should().Be(Key("Ctrl+Alt+L"));
        viewModel.Lock.Problem.Should().BeNull();
    }

    [Theory]
    [InlineData(HotkeyModifiers.Shift, VK_L, "needs Ctrl, Alt or Win")]
    [InlineData(HotkeyModifiers.Control, VK_F12, "reserved")]
    public void A_rejected_combination_keeps_recording_and_sets_nothing(HotkeyModifiers modifiers, uint virtualKey, string reasonPart)
    {
        var viewModel = Create(new HotkeySettings("Ctrl+Alt+K", null));
        viewModel.Lock.RecordCommand.Execute(null);

        viewModel.Lock.Capture(modifiers, virtualKey);

        viewModel.Lock.Problem.Should().Contain(reasonPart);
        viewModel.Lock.IsRecording.Should().BeTrue();
        viewModel.Lock.Hotkey.Should().Be(Key("Ctrl+Alt+K"));
        _calls.Should().Equal("suspend");
    }

    [Fact]
    public void The_other_actions_combination_is_refused_by_name()
    {
        var viewModel = Create(new HotkeySettings("Ctrl+Alt+L", "Ctrl+Alt+H"));
        viewModel.Overlay.RecordCommand.Execute(null);

        viewModel.Overlay.Capture(CtrlAlt, VK_L);

        viewModel.Overlay.Problem.Should().Be($"Ctrl+Alt+L is already used for {HotkeyEditor.LockLabel}.");
        viewModel.Overlay.Hotkey.Should().Be(Key("Ctrl+Alt+H"));
        viewModel.Overlay.IsRecording.Should().BeFalse();
        _set.Should().BeEmpty();
        _calls.Should().Equal("suspend", "resume");
    }

    [Fact]
    public void Its_own_combination_again_is_no_conflict()
    {
        var viewModel = Create(new HotkeySettings("Ctrl+Alt+L", null));
        viewModel.Lock.RecordCommand.Execute(null);

        viewModel.Lock.Capture(CtrlAlt, VK_L);

        viewModel.Lock.Problem.Should().BeNull();
        _set.Should().ContainSingle();
    }

    [Fact]
    public void A_combination_taken_by_another_program_is_refused_and_the_old_one_stays()
    {
        var viewModel = Create(new HotkeySettings("Ctrl+Alt+K", null));
        _refusal = "Ctrl+Alt+L is in use by another program; pick another one.";
        viewModel.Lock.RecordCommand.Execute(null);

        viewModel.Lock.Capture(CtrlAlt, VK_L);

        viewModel.Lock.Problem.Should().Be(_refusal);
        viewModel.Lock.Hotkey.Should().Be(Key("Ctrl+Alt+K"));
        viewModel.Lock.Display.Should().Be("Ctrl+Alt+K");
    }

    [Fact]
    public void Cancel_resumes_and_keeps_the_hotkey()
    {
        var viewModel = Create(new HotkeySettings("Ctrl+Alt+K", null));
        viewModel.Lock.RecordCommand.Execute(null);

        viewModel.Lock.Cancel();

        viewModel.Lock.IsRecording.Should().BeFalse();
        viewModel.Lock.Hotkey.Should().Be(Key("Ctrl+Alt+K"));
        _calls.Should().Equal("suspend", "resume");
    }

    [Fact]
    public void Cancel_without_recording_does_nothing()
    {
        var viewModel = Create();

        viewModel.Lock.Cancel();

        _calls.Should().BeEmpty();
    }

    [Fact]
    public void Recording_twice_suspends_once()
    {
        var viewModel = Create();

        viewModel.Lock.RecordCommand.Execute(null);
        viewModel.Lock.RecordCommand.Execute(null);

        _calls.Should().Equal("suspend");
    }

    [Fact]
    public void Clear_sets_no_hotkey()
    {
        var viewModel = Create(new HotkeySettings("Ctrl+Alt+K", "Ctrl+Alt+H"));

        viewModel.Overlay.ClearCommand.Execute(null);

        _set.Should().Equal((HotkeyAction.ToggleOverlay, (Hotkey?)null));
        viewModel.Overlay.Hotkey.Should().BeNull();
        viewModel.Overlay.Display.Should().Be(HotkeyEditor.NotSetText);
        viewModel.Lock.Hotkey.Should().Be(Key("Ctrl+Alt+K"));
    }

    [Fact]
    public void Clear_while_recording_ends_the_recording_first()
    {
        var viewModel = Create(new HotkeySettings("Ctrl+Alt+K", null));
        viewModel.Lock.RecordCommand.Execute(null);

        viewModel.Lock.ClearCommand.Execute(null);

        _calls.Should().Equal("suspend", "resume", "set");
        viewModel.Lock.IsRecording.Should().BeFalse();
    }

    [Fact]
    public void Clear_removes_the_problem_of_an_invalid_file_hotkey()
    {
        var viewModel = Create(new HotkeySettings("Shift+L", null));

        viewModel.Lock.ClearCommand.Execute(null);

        viewModel.Lock.Problem.Should().BeNull();
    }

    [Fact]
    public void Starting_a_recording_clears_the_last_problem()
    {
        var viewModel = Create();
        viewModel.Lock.RecordCommand.Execute(null);
        viewModel.Lock.Capture(HotkeyModifiers.Shift, VK_L);
        viewModel.Lock.Cancel();

        viewModel.Lock.RecordCommand.Execute(null);

        viewModel.Lock.Problem.Should().BeNull();
    }

    [Fact]
    public void Every_action_has_its_label()
    {
        var viewModel = Create();

        viewModel.Lock.Action.Should().Be(HotkeyAction.ToggleLock);
        viewModel.Lock.Label.Should().Be(HotkeyEditor.LockLabel);
        viewModel.Overlay.Action.Should().Be(HotkeyAction.ToggleOverlay);
        viewModel.Overlay.Label.Should().Be(HotkeyEditor.OverlayLabel);
        viewModel.Sync.Action.Should().Be(HotkeyAction.SyncContractTracker);
        viewModel.Sync.Label.Should().Be("Sync from ContractTracker");
    }

    [Fact]
    public void The_sync_hotkey_is_not_set_by_default_and_comes_from_the_file()
    {
        // No defaults (D17): without an entry there is no hotkey.
        Create().Sync.Hotkey.Should().BeNull();
        Create(new HotkeySettings(SyncContractTracker: "Ctrl+Alt+S")).Sync.Display.Should().Be("Ctrl+Alt+S");
    }

    [Fact]
    public void The_lock_combination_is_refused_for_the_sync_and_the_other_way_round()
    {
        var viewModel = Create(new HotkeySettings("Ctrl+Alt+L", null, "Ctrl+Alt+H"));

        viewModel.Sync.RecordCommand.Execute(null);
        viewModel.Sync.Capture(CtrlAlt, VK_L);
        viewModel.Overlay.RecordCommand.Execute(null);
        viewModel.Overlay.Capture(CtrlAlt, VK_H);

        viewModel.Sync.Problem.Should().Be($"Ctrl+Alt+L is already used for {HotkeyEditor.LockLabel}.");
        viewModel.Overlay.Problem.Should().Be($"Ctrl+Alt+H is already used for {HotkeyEditor.SyncLabel}.");
        _set.Should().BeEmpty();
    }

    [Fact]
    public void The_hint_warns_about_windows_combinations()
    {
        HotkeysViewModel.Hint.Should().Contain("Ctrl, Alt or Win required").And.Contain("Win combinations may be taken by Windows itself");
    }

    [Fact]
    public void A_set_hotkey_of_the_other_action_counts_for_the_conflict_check()
    {
        var viewModel = Create();
        viewModel.Lock.RecordCommand.Execute(null);
        viewModel.Lock.Capture(CtrlAlt, VK_H);

        viewModel.Overlay.RecordCommand.Execute(null);
        viewModel.Overlay.Capture(CtrlAlt, VK_H);

        viewModel.Overlay.Problem.Should().Contain("already used for");
    }
}
