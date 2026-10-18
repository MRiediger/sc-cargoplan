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

        hotkey.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.NoRepeat);
        hotkey.VirtualKey.Should().Be(0x4Cu);
    }

    [Fact]
    public void Always_sets_NoRepeat_so_holding_the_keys_toggles_once()
    {
        Parse("Alt+H").Modifiers.Should().HaveFlag(HotkeyModifiers.NoRepeat);
    }

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
    [InlineData("ctrl+space", "Ctrl+Space")]
    [InlineData("alt+scrolllock", "Alt+ScrollLock")]
    [InlineData("Ctrl+Alt+Shift+Win+7", "Ctrl+Alt+Shift+Win+7")]
    public void ToString_is_canonical_without_NoRepeat(string text, string expected)
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
    [InlineData("Ctrl+Space", 0x20u)]
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
    [InlineData("Ctrl+Ä", "unknown key")]
    [InlineData("Ctrl+F25", "unknown key")]
    [InlineData("Hyper+L", "unknown modifier")]
    [InlineData("Ctrl+Ctrl+L", "twice")]
    [InlineData("Ctrl+L+K", "one key")]
    [InlineData("Ctrl++L", "empty part")]
    [InlineData("Ctrl+F12", "reserved")]
    public void Rejects_invalid_text_with_a_reason(string text, string reasonPart)
    {
        HotkeyParser.TryParse(text, out var hotkey, out var error).Should().BeFalse();

        hotkey.Should().BeNull();
        error.Should().Contain(reasonPart);
    }
}
