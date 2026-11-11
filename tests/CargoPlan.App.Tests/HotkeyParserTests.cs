using CargoPlan.App.Hotkeys;

namespace CargoPlan.App.Tests;

public class HotkeyParserTests
{
    private static Hotkey Parse(string text)
    {
        HotkeyParser.TryParse(text, out var hotkey, out var error).Should().BeTrue(error);
        return hotkey!;
    }

    [Fact]
    public void Parses_modifiers_and_key()
    {
        var hotkey = Parse("Ctrl+Alt+L");

        hotkey.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        hotkey.VirtualKey.Should().Be(0x4Cu);
    }

    // NoRepeat is gone (F15): "holding the keys toggles once" is the poller's, HotkeyPollerTests.Holding_the_keys_fires_no_second_time.

    [Theory]
    [InlineData("ctrl+alt+l")]
    [InlineData("CTRL+ALT+L")]
    [InlineData(" Ctrl + Alt + L ")]
    [InlineData("Alt+Ctrl+L")]
    public void Ignores_case_spaces_and_modifier_order(string text)
    {
        Parse(text).Should().Be(Parse("Ctrl+Alt+L"));
    }

    [Theory]
    [InlineData("alt+ctrl+l", "Ctrl+Alt+L")]
    [InlineData("win+shift+f5", "Shift+Win+F5")]
    [InlineData("ctrl+insert", "Ctrl+Insert")]
    [InlineData("alt+scrolllock", "Alt+ScrollLock")]
    [InlineData("Ctrl+Alt+Shift+Win+7", "Ctrl+Alt+Shift+Win+7")]
    public void ToString_is_canonical(string text, string expected)
    {
        Parse(text).ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("alt+ctrl+l")]
    [InlineData("ctrl+pagedown")]
    [InlineData("win+f24")]
    public void ToString_parses_back_to_the_same_hotkey(string text)
    {
        var hotkey = Parse(text);

        Parse(hotkey.ToString()).Should().Be(hotkey);
    }

    // Values from https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    [Theory]
    [InlineData("Ctrl+A", 0x41u)]
    [InlineData("Ctrl+Z", 0x5Au)]
    [InlineData("Ctrl+0", 0x30u)]
    [InlineData("Ctrl+9", 0x39u)]
    [InlineData("Ctrl+F1", 0x70u)]
    [InlineData("Ctrl+F11", 0x7Au)]
    [InlineData("Ctrl+F13", 0x7Cu)]
    [InlineData("Ctrl+F24", 0x87u)]
    [InlineData("Ctrl+Pause", 0x13u)]
    [InlineData("Ctrl+ScrollLock", 0x91u)]
    [InlineData("Ctrl+Insert", 0x2Du)]
    [InlineData("Ctrl+Delete", 0x2Eu)]
    [InlineData("Ctrl+Home", 0x24u)]
    [InlineData("Ctrl+End", 0x23u)]
    [InlineData("Ctrl+PageUp", 0x21u)]
    [InlineData("Ctrl+PageDown", 0x22u)]
    public void Maps_the_key_to_its_virtual_key_code(string text, uint expected)
    {
        Parse(text).VirtualKey.Should().Be(expected);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("Ctrl+Alt", "no key")]
    [InlineData("L", "Ctrl, Alt or Win")]
    [InlineData("Shift+L", "Ctrl, Alt or Win")]
    [InlineData("7", "Ctrl, Alt or Win")]
    [InlineData("Delete", "Ctrl, Alt or Win")]
    [InlineData("F12", "reserved")]
    [InlineData("Shift+F12", "reserved")]
    [InlineData("Ctrl+Ä", "unknown key")]
    [InlineData("Ctrl+F25", "unknown key")]
    [InlineData("Hyper+L", "unknown modifier")]
    [InlineData("Ctrl+Ctrl+L", "twice")]
    [InlineData("Ctrl+L+K", "one key")]
    [InlineData("Ctrl++L", "empty part")]
    [InlineData("Ctrl+F12", "reserved")]
    [InlineData("Ctrl+Space", "reserved")]
    [InlineData("alt+space", "reserved")]
    public void Rejects_invalid_text_with_a_reason(string text, string reasonPart)
    {
        HotkeyParser.TryParse(text, out var hotkey, out var error).Should().BeFalse();

        hotkey.Should().BeNull();
        error.Should().Contain(reasonPart);
    }

    // Recorded keys (T4.4): the same rules as typed text.

    private const uint VK_L = 0x4C;
    private const uint VK_F3 = 0x72;
    private const uint VK_F12 = 0x7B;
    private const uint VK_F13 = 0x7C;
    private const uint VK_SPACE = 0x20;
    private const uint VK_OEM_COMMA = 0xBC;
    private const uint VK_CONTROL = 0x11;

    [Fact]
    public void Creates_a_recorded_hotkey_equal_to_the_typed_one()
    {
        HotkeyParser.TryCreate(HotkeyModifiers.Control | HotkeyModifiers.Alt, VK_L, out var hotkey, out var error).Should().BeTrue(error);

        hotkey.Should().Be(Parse("Ctrl+Alt+L"));
        hotkey!.ToString().Should().Be("Ctrl+Alt+L");
    }

    [Theory]
    [InlineData(HotkeyModifiers.Shift, VK_L, "needs Ctrl, Alt or Win")]
    [InlineData(HotkeyModifiers.None, VK_L, "needs Ctrl, Alt or Win")]
    [InlineData(HotkeyModifiers.Control, VK_F12, "reserved")]
    [InlineData(HotkeyModifiers.None, VK_F12, "reserved")]
    [InlineData(HotkeyModifiers.Control, VK_SPACE, "reserved")]
    [InlineData(HotkeyModifiers.Control, VK_OEM_COMMA, "unknown key")]
    [InlineData(HotkeyModifiers.Control, VK_CONTROL, "unknown key")]
    public void Rejects_recorded_keys_with_the_reasons_of_typed_text(HotkeyModifiers modifiers, uint virtualKey, string reasonPart)
    {
        HotkeyParser.TryCreate(modifiers, virtualKey, out var hotkey, out var error).Should().BeFalse();

        hotkey.Should().BeNull();
        error.Should().Contain(reasonPart);
    }

    [Fact]
    public void Shift_L_is_rejected_with_the_same_reason_typed_and_recorded()
    {
        HotkeyParser.TryParse("Shift+L", out _, out var typed);
        HotkeyParser.TryCreate(HotkeyModifiers.Shift, VK_L, out _, out var recorded);

        recorded.Should().Be(typed);
    }

    // D97: a polled key isn't taken from other programs, it reaches the game too; the reason must say so.
    [Fact]
    public void A_key_without_Ctrl_Alt_or_Win_is_refused_because_it_reaches_the_game()
    {
        HotkeyParser.TryParse("Shift+L", out _, out var error);

        error.Should().Contain("reaches the game").And.NotContain("every program");
    }

    [Fact]
    public void Space_is_rejected_with_the_same_reason_typed_and_recorded()
    {
        HotkeyParser.TryParse("Ctrl+Space", out _, out var typed);
        HotkeyParser.TryCreate(HotkeyModifiers.Control, VK_SPACE, out _, out var recorded);

        recorded.Should().Be(typed);
    }

    // D104: F keys stand alone; they never type into Star Citizen's chat. F12 stays reserved.
    [Theory]
    [InlineData("F1", 0x70u, HotkeyModifiers.None)]
    [InlineData("F3", 0x72u, HotkeyModifiers.None)]
    [InlineData("f11", 0x7Au, HotkeyModifiers.None)]
    [InlineData("F13", 0x7Cu, HotkeyModifiers.None)]
    [InlineData("F24", 0x87u, HotkeyModifiers.None)]
    [InlineData("Shift+F3", 0x72u, HotkeyModifiers.Shift)]
    public void An_F_key_needs_no_modifier(string text, uint virtualKey, HotkeyModifiers modifiers)
    {
        Parse(text).Should().Be(new Hotkey(modifiers, virtualKey));
    }

    [Theory]
    [InlineData(HotkeyModifiers.None, VK_F3)]
    [InlineData(HotkeyModifiers.Shift, VK_F3)]
    [InlineData(HotkeyModifiers.None, VK_F13)]
    public void A_recorded_F_key_needs_no_modifier(HotkeyModifiers modifiers, uint virtualKey)
    {
        HotkeyParser.TryCreate(modifiers, virtualKey, out var hotkey, out var error).Should().BeTrue(error);

        hotkey.Should().Be(new Hotkey(modifiers, virtualKey));
    }

    [Fact]
    public void A_bare_F_key_reads_back_as_its_name()
    {
        Parse("f3").ToString().Should().Be("F3");
    }

    [Fact]
    public void The_reason_for_a_bare_letter_names_the_F_keys()
    {
        HotkeyParser.TryParse("L", out _, out var error);

        error.Should().Contain("unless it is an F key");
    }
}
