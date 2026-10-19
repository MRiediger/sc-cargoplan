using System.Diagnostics.CodeAnalysis;

namespace CargoPlan.App.Hotkeys;

/// <summary>
/// Reads a hotkey from settings.json ("Ctrl+Alt+L") or from keys pressed in the settings window (T4.4), with the same
/// rules and reasons either way. Pure functions; registering is <see cref="HotkeyService"/>'s job.
/// Own small key table instead of WPF's KeyGestureConverter: our own error reasons for the notice line, and
/// only keys that make sense next to a game (D17).
/// </summary>
public static class HotkeyParser
{
    /// <summary>Modifier names in canonical order, as <see cref="Hotkey.ToString"/> writes them.</summary>
    public static IReadOnlyList<(string Name, HotkeyModifiers Flag)> ModifierNames { get; } =
    [
        ("Ctrl", HotkeyModifiers.Control),
        ("Alt", HotkeyModifiers.Alt),
        ("Shift", HotkeyModifiers.Shift),
        ("Win", HotkeyModifiers.Win),
    ];

    // Ctrl, Alt or Win must be part of it: a bare key or Shift+key (a capital letter) would be taken from every
    // program, including Star Citizen's chat.
    private const HotkeyModifiers RequiredModifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Win;

    // Source: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    private const uint VK_PAUSE = 0x13;
    private const uint VK_SPACE = 0x20;
    private const uint VK_PRIOR = 0x21; // Page up
    private const uint VK_NEXT = 0x22;  // Page down
    private const uint VK_END = 0x23;
    private const uint VK_HOME = 0x24;
    private const uint VK_INSERT = 0x2D;
    private const uint VK_DELETE = 0x2E;
    private const uint VK_0 = 0x30;     // '0'..'9' are 0x30..0x39
    private const uint VK_A = 0x41;     // 'A'..'Z' are 0x41..0x5A
    private const uint VK_F1 = 0x70;    // F1..F24 are 0x70..0x87
    private const uint VK_F12 = 0x7B;
    private const uint VK_SCROLL = 0x91;
    private const int FunctionKeyCount = 24;

    // "The F12 key is reserved for use by the debugger at all times, so it should not be registered as a hot key."
    // Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey (Remarks)
    private const string ReservedKey = "F12";

    private const string ReservedReason = $"{ReservedKey} is reserved for debuggers by Windows";

    /// <summary>Canonical key name → virtual-key code; lookup ignores case.</summary>
    private static readonly Dictionary<string, uint> Keys = BuildKeys();

    private static readonly Dictionary<uint, string> KeyNames = Keys.ToDictionary(k => k.Value, k => k.Key);

    /// <summary>
    /// "Ctrl+Alt+L" → modifiers plus key, always with NoRepeat. Case and spaces around '+' don't matter.
    /// Invalid → false and a reason for the notice line.
    /// </summary>
    public static bool TryParse(string text, [NotNullWhen(true)] out Hotkey? hotkey, [NotNullWhen(false)] out string? error)
    {
        var (reason, parsed) = Parse(text);
        if (parsed is not null)
        {
            (hotkey, error) = (parsed, null);
            return true;
        }

        (hotkey, error) = (null, reason!); // Parse returns exactly one of the two.
        return false;
    }

    /// <summary>
    /// Keys pressed in the settings window (T4.4): the same checks as <see cref="TryParse"/>, in the same order, with the
    /// same reasons, so Shift+L and F12 are refused alike whether typed or pressed.
    /// </summary>
    /// <param name="modifiers">NoRepeat is ignored; every hotkey gets it.</param>
    public static bool TryCreate(HotkeyModifiers modifiers, uint virtualKey, [NotNullWhen(true)] out Hotkey? hotkey, [NotNullWhen(false)] out string? error)
    {
        var (reason, created) = virtualKey == VK_F12 ? (ReservedReason, null)
            : !KeyNames.ContainsKey(virtualKey) ? (UnknownKeyReason(null), null)
            : Check(modifiers & ~HotkeyModifiers.NoRepeat, virtualKey);
        if (created is not null)
        {
            (hotkey, error) = (created, null);
            return true;
        }

        (hotkey, error) = (null, reason!); // Each branch gives exactly one of the two.
        return false;
    }

    /// <summary>Canonical name of a key from the table, e.g. "L", "F5", "PageUp".</summary>
    public static string KeyName(uint virtualKey) => KeyNames[virtualKey];

    private static (string? Error, Hotkey? Hotkey) Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ("the text is empty", null);
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Any(p => p.Length == 0))
        {
            return ("it has an empty part between '+' signs", null);
        }

        var modifiers = HotkeyModifiers.None;
        foreach (var part in parts[..^1])
        {
            if (FindModifier(part) is not { } flag)
            {
                return (Keys.ContainsKey(part)
                    ? $"only one key is allowed, after the modifiers (\"{part}\")"
                    : $"unknown modifier \"{part}\" (use Ctrl, Alt, Shift, Win)", null);
            }

            if (modifiers.HasFlag(flag))
            {
                return ($"\"{part}\" appears twice", null);
            }

            modifiers |= flag;
        }

        var key = parts[^1];
        if (FindModifier(key) is not null)
        {
            return ("there is no key after the modifiers", null);
        }

        if (key.Equals(ReservedKey, StringComparison.OrdinalIgnoreCase))
        {
            return (ReservedReason, null);
        }

        if (!Keys.TryGetValue(key, out var virtualKey))
        {
            return (UnknownKeyReason(key), null);
        }

        return Check(modifiers, virtualKey);
    }

    /// <param name="key">As typed; null for a pressed key, which has no name of ours.</param>
    private static string UnknownKeyReason(string? key) =>
        $"unknown key{(key is null ? "" : $" \"{key}\"")} (use A-Z, 0-9, F1-F24, Space, Pause, ScrollLock, Insert, Delete, Home, End, PageUp, PageDown)";

    /// <summary>The rule both ways share once the key is known: Ctrl, Alt or Win must be part of it.</summary>
    private static (string? Error, Hotkey? Hotkey) Check(HotkeyModifiers modifiers, uint virtualKey) =>
        (modifiers & RequiredModifiers) == HotkeyModifiers.None
            ? ("it needs Ctrl, Alt or Win, or it would take the key from every program", null)
            : (null, new Hotkey(modifiers | HotkeyModifiers.NoRepeat, virtualKey));

    private static HotkeyModifiers? FindModifier(string name)
    {
        foreach (var (modifierName, flag) in ModifierNames)
        {
            if (name.Equals(modifierName, StringComparison.OrdinalIgnoreCase))
            {
                return flag;
            }
        }

        return null;
    }

    private static Dictionary<string, uint> BuildKeys()
    {
        Dictionary<string, uint> keys = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Space"] = VK_SPACE,
            ["Pause"] = VK_PAUSE,
            ["ScrollLock"] = VK_SCROLL,
            ["Insert"] = VK_INSERT,
            ["Delete"] = VK_DELETE,
            ["Home"] = VK_HOME,
            ["End"] = VK_END,
            ["PageUp"] = VK_PRIOR,
            ["PageDown"] = VK_NEXT,
        };

        for (var i = 0u; i < 26; i++)
        {
            keys[((char)('A' + i)).ToString()] = VK_A + i;
        }

        for (var i = 0u; i < 10; i++)
        {
            keys[i.ToString()] = VK_0 + i;
        }

        for (var i = 0u; i < FunctionKeyCount; i++)
        {
            keys[$"F{i + 1}"] = VK_F1 + i;
        }

        keys.Remove(ReservedKey);
        return keys;
    }
}
