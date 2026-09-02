using System.Globalization;
using System.Windows.Input;

namespace ManagerIV.Core;

/// <summary>
/// Helper for converting between Windows Virtual-Key (VK) codes, mouse buttons, and human-readable key names.
/// Consumed by FusionFix settings and other mod keybindings.
/// </summary>
public static class VirtualKeyHelper
{
    public const string UnboundHex = "0x00";

    public static int ParseVirtualKey(string? hexOrDec)
    {
        if (string.IsNullOrWhiteSpace(hexOrDec)) return 0;
        hexOrDec = hexOrDec.Trim();

        if (hexOrDec.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(hexOrDec[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexVal))
            {
                return hexVal;
            }
        }
        else if (int.TryParse(hexOrDec, NumberStyles.Integer, CultureInfo.InvariantCulture, out int decVal))
        {
            return decVal;
        }

        return 0;
    }

    public static string FormatVirtualKey(int vk)
    {
        if (vk <= 0) return UnboundHex;
        return $"0x{vk:X2}";
    }

    public static string GetKeyDisplayName(string? hexOrDec)
    {
        int vk = ParseVirtualKey(hexOrDec);
        return GetKeyDisplayName(vk);
    }

    public static string GetKeyDisplayName(int vk)
    {
        return vk switch
        {
            0x00 => "Unbound",

            // Mouse Buttons
            0x01 => "Left Click",
            0x02 => "Right Click",
            0x04 => "Middle Click",
            0x05 => "Mouse 4 (Thumb Back)",
            0x06 => "Mouse 5 (Thumb Forward)",

            // Common Controls & Modifiers
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0C => "Clear",
            0x0D => "Enter",
            0x10 => "Shift",
            0x11 => "Ctrl",
            0x12 => "Alt",
            0x13 => "Pause",
            0x14 => "Caps Lock",
            0x1B => "Escape",
            0x20 => "Space",
            0x21 => "Page Up",
            0x22 => "Page Down",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left Arrow",
            0x26 => "Up Arrow",
            0x27 => "Right Arrow",
            0x28 => "Down Arrow",
            0x2C => "Print Screen",
            0x2D => "Insert",
            0x2E => "Delete",

            // Digits 0-9
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),

            // Letters A-Z
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),

            // Windows keys & context menu
            0x5B => "Left Windows",
            0x5C => "Right Windows",
            0x5D => "Apps / Menu",

            // Numpad
            0x60 => "Num 0",
            0x61 => "Num 1",
            0x62 => "Num 2",
            0x63 => "Num 3",
            0x64 => "Num 4",
            0x65 => "Num 5",
            0x66 => "Num 6",
            0x67 => "Num 7",
            0x68 => "Num 8",
            0x69 => "Num 9",
            0x6A => "Num *",
            0x6B => "Num +",
            0x6C => "Num Separator",
            0x6D => "Num -",
            0x6E => "Num .",
            0x6F => "Num /",

            // Function Keys
            >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",

            // Lock keys
            0x90 => "Num Lock",
            0x91 => "Scroll Lock",

            // Specific Left/Right Modifiers
            0xA0 => "Left Shift",
            0xA1 => "Right Shift",
            0xA2 => "Left Ctrl",
            0xA3 => "Right Ctrl",
            0xA4 => "Left Alt",
            0xA5 => "Right Alt",

            // OEM Punctuation (US Layout standard reference)
            0xBA => "; (Semicolon)",
            0xBB => "= (Equals)",
            0xBC => ", (Comma)",
            0xBD => "- (Minus)",
            0xBE => ". (Period)",
            0xBF => "/ (Slash)",
            0xC0 => "` (Tilde)",
            0xDB => "[ (Left Bracket)",
            0xDC => "\\ (Backslash)",
            0xDD => "] (Right Bracket)",
            0xDE => "' (Quote)",

            _ => GetFallbackKeyName(vk)
        };
    }

    private static string GetFallbackKeyName(int vk)
    {
        try
        {
            var key = KeyInterop.KeyFromVirtualKey(vk);
            if (key != Key.None)
            {
                return key.ToString();
            }
        }
        catch
        {
            // Ignore interop conversion failure
        }

        return $"0x{vk:X2}";
    }

    /// <summary>
    /// Normalizes specific left/right modifiers to generic modifiers if preferred for game input compatibility.
    /// </summary>
    public static int NormalizeModifierVirtualKey(int vk)
    {
        return vk switch
        {
            0xA0 or 0xA1 => 0x10, // VK_SHIFT
            0xA2 or 0xA3 => 0x11, // VK_CONTROL
            0xA4 or 0xA5 => 0x12, // VK_MENU (Alt)
            _ => vk
        };
    }
}
