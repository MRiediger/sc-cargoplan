using System.Windows.Input;
using CargoPlan.App.Hotkeys;

namespace CargoPlan.App.Tests;

public class RecordedKeyTests
{
    private const uint VK_L = 0x4C;
    private const uint VK_F5 = 0x74;

    [Fact]
    public void A_key_with_modifiers_gives_both()
    {
        RecordedKey.From(Key.L, Key.None, ModifierKeys.Control | ModifierKeys.Alt)
            .Should().Be((HotkeyModifiers.Control | HotkeyModifiers.Alt, VK_L));
    }

    /// <summary>With Alt alone Windows sends a system key: WPF reports Key.System and the real key in SystemKey.</summary>
    [Fact]
    public void A_system_key_takes_the_real_key()
    {
        RecordedKey.From(Key.System, Key.F5, ModifierKeys.Alt).Should().Be((HotkeyModifiers.Alt, VK_F5));
    }

    [Fact]
    public void Every_modifier_maps()
    {
        RecordedKey.From(Key.L, Key.None, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows)!.Value.Modifiers
            .Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win);
    }

    [Theory]
    [InlineData(Key.LeftCtrl, Key.None)]
    [InlineData(Key.RightCtrl, Key.None)]
    [InlineData(Key.LeftShift, Key.None)]
    [InlineData(Key.RightShift, Key.None)]
    [InlineData(Key.LWin, Key.None)]
    [InlineData(Key.RWin, Key.None)]
    [InlineData(Key.System, Key.LeftAlt)]
    [InlineData(Key.System, Key.RightAlt)]
    public void A_modifier_alone_is_no_combination_yet(Key key, Key systemKey)
    {
        RecordedKey.From(key, systemKey, ModifierKeys.Control).Should().BeNull();
    }
}
