using CargoPlan.App.Hotkeys;

namespace CargoPlan.App.Tests;

/// <summary>The polled hotkeys (D97): one press fires once, modifiers as saved, nothing while not armed.</summary>
public class HotkeyPollerTests
{
    // Source: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    private const uint VK_SHIFT = 0x10;
    private const uint VK_CONTROL = 0x11;
    private const uint VK_MENU = 0x12;
    private const uint VK_K = 0x4B;
    private const uint VK_L = 0x4C;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;

    private readonly HashSet<uint> _down = [];
    private readonly HotkeyPoller _poller;

    public HotkeyPollerTests() => _poller = new HotkeyPoller(_down.Contains);

    private static Hotkey Parse(string text)
    {
        HotkeyParser.TryParse(text, out var hotkey, out var error).Should().BeTrue(error);
        return hotkey!;
    }

    private void Press(params uint[] keys) => _down.UnionWith(keys);

    private void Release(params uint[] keys) => _down.ExceptWith(keys);

    [Fact]
    public void A_press_fires_once()
    {
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+L"));
        _poller.Poll(armed: true).Should().BeEmpty();

        Press(VK_CONTROL, VK_MENU, VK_L);

        _poller.Poll(armed: true).Should().Equal(HotkeyAction.ToggleLock);
    }

    [Fact]
    public void Holding_the_keys_fires_no_second_time()
    {
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+L"));
        Press(VK_CONTROL, VK_MENU, VK_L);
        _poller.Poll(armed: true);

        _poller.Poll(armed: true).Should().BeEmpty();
        _poller.Poll(armed: true).Should().BeEmpty();
    }

    [Fact]
    public void Releasing_and_pressing_again_fires_again()
    {
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+L"));
        Press(VK_CONTROL, VK_MENU, VK_L);
        _poller.Poll(armed: true);

        Release(VK_L);
        _poller.Poll(armed: true).Should().BeEmpty();
        Press(VK_L);

        _poller.Poll(armed: true).Should().Equal(HotkeyAction.ToggleLock);
    }

    [Fact]
    public void A_missing_modifier_fires_nothing()
    {
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+L"));

        Press(VK_CONTROL, VK_L);

        _poller.Poll(armed: true).Should().BeEmpty();
    }

    [Fact]
    public void An_extra_modifier_fires_nothing()
    {
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+L"));

        Press(VK_CONTROL, VK_MENU, VK_SHIFT, VK_L);

        _poller.Poll(armed: true).Should().BeEmpty("Ctrl+Alt+Shift+L is another combination, as with RegisterHotKey");
    }

    [Fact]
    public void Modifiers_pressed_after_the_key_fire_nothing()
    {
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+L"));
        Press(VK_L);
        _poller.Poll(armed: true);

        Press(VK_CONTROL, VK_MENU);

        _poller.Poll(armed: true).Should().BeEmpty("only the key's own press fires");
    }

    [Theory]
    [InlineData(VK_LWIN)]
    [InlineData(VK_RWIN)]
    public void Win_is_either_windows_key(uint winKey)
    {
        _poller.Set(HotkeyAction.ResetView, Parse("Win+K"));

        Press(winKey, VK_K);

        _poller.Poll(armed: true).Should().Equal(HotkeyAction.ResetView);
    }

    [Fact]
    public void Not_armed_fires_nothing()
    {
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+L"));

        Press(VK_CONTROL, VK_MENU, VK_L);

        _poller.Poll(armed: false).Should().BeEmpty();
    }

    [Fact]
    public void Keys_held_while_not_armed_fire_nothing_once_armed()
    {
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+L"));
        Press(VK_CONTROL, VK_MENU, VK_L);
        _poller.Poll(armed: false);

        _poller.Poll(armed: true).Should().BeEmpty("switching to the game with the keys held is no press");
    }

    [Fact]
    public void A_hotkey_set_while_its_keys_are_held_fires_only_on_the_next_press()
    {
        // The end of a recording: the keys just recorded are still down.
        Press(VK_CONTROL, VK_MENU, VK_L);
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+L"));

        _poller.Poll(armed: true).Should().BeEmpty();

        Release(VK_L);
        _poller.Poll(armed: true);
        Press(VK_L);
        _poller.Poll(armed: true).Should().Equal(HotkeyAction.ToggleLock);
    }

    [Fact]
    public void Set_null_removes_the_hotkey()
    {
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+L"));
        _poller.Set(HotkeyAction.ToggleLock, null);

        Press(VK_CONTROL, VK_MENU, VK_L);

        _poller.Poll(armed: true).Should().BeEmpty();
    }

    [Fact]
    public void Set_replaces_the_old_combination()
    {
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+L"));
        _poller.Set(HotkeyAction.ToggleLock, Parse("Ctrl+Alt+K"));

        Press(VK_CONTROL, VK_MENU, VK_L);
        _poller.Poll(armed: true).Should().BeEmpty();
        Press(VK_K);

        _poller.Poll(armed: true).Should().Equal(HotkeyAction.ToggleLock);
    }

    [Fact]
    public void Two_hotkeys_on_one_key_fire_by_their_modifiers()
    {
        _poller.Set(HotkeyAction.Undo, Parse("Ctrl+Alt+K"));
        _poller.Set(HotkeyAction.Redo, Parse("Ctrl+Alt+Shift+K"));

        Press(VK_CONTROL, VK_MENU, VK_SHIFT, VK_K);

        _poller.Poll(armed: true).Should().Equal(HotkeyAction.Redo);
    }

    // Armed (D97): Star Citizen or CargoPlan in front, not suspended, no text box with the focus in our own window.

    [Fact]
    public void Armed_with_the_game_in_front() =>
        HotkeyPoller.IsArmed(suspended: false, textInputFocused: false, ownProcessInFront: false, "StarCitizen").Should().BeTrue();

    [Fact]
    public void Armed_with_our_own_window_in_front() =>
        HotkeyPoller.IsArmed(suspended: false, textInputFocused: false, ownProcessInFront: true, "SC-CargoPlan").Should().BeTrue();

    [Theory]
    [InlineData("msedge")]
    [InlineData("EXCEL")]
    [InlineData(null)]
    public void Not_armed_with_another_program_in_front(string? processName) =>
        HotkeyPoller.IsArmed(suspended: false, textInputFocused: false, ownProcessInFront: false, processName).Should().BeFalse();

    [Fact]
    public void Not_armed_while_suspended_for_recording() =>
        HotkeyPoller.IsArmed(suspended: true, textInputFocused: false, ownProcessInFront: true, "SC-CargoPlan").Should().BeFalse();

    [Fact]
    public void Not_armed_while_a_text_box_of_ours_has_the_focus() =>
        HotkeyPoller.IsArmed(suspended: false, textInputFocused: true, ownProcessInFront: true, "SC-CargoPlan").Should().BeFalse();

    [Fact]
    public void A_text_box_focus_left_over_counts_not_with_the_game_in_front() =>
        // WPF may still name the editor cell after the switch to the game; nobody types there (K2).
        HotkeyPoller.IsArmed(suspended: false, textInputFocused: true, ownProcessInFront: false, "StarCitizen").Should().BeTrue();
}
