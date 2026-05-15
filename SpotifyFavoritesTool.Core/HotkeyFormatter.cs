namespace SpotifyFavoritesTool;

public static class HotkeyFormatter
{
    private static readonly Dictionary<uint, string> VirtualKeyNames = new()
    {
        [0x08] = "VK_BACK",
        [0x09] = "VK_TAB",
        [0x0D] = "VK_RETURN",
        [0x10] = "VK_SHIFT",
        [0x11] = "VK_CONTROL",
        [0x12] = "VK_MENU",
        [0x13] = "VK_PAUSE",
        [0x14] = "VK_CAPITAL",
        [0x1B] = "VK_ESCAPE",
        [0x20] = "VK_SPACE",
        [0x21] = "VK_PRIOR",
        [0x22] = "VK_NEXT",
        [0x23] = "VK_END",
        [0x24] = "VK_HOME",
        [0x25] = "VK_LEFT",
        [0x26] = "VK_UP",
        [0x27] = "VK_RIGHT",
        [0x28] = "VK_DOWN",
        [0x2C] = "VK_SNAPSHOT",
        [0x2D] = "VK_INSERT",
        [0x2E] = "VK_DELETE",
        [0x5B] = "VK_LWIN",
        [0x5C] = "VK_RWIN",
        [0x60] = "VK_NUMPAD0",
        [0x61] = "VK_NUMPAD1",
        [0x62] = "VK_NUMPAD2",
        [0x63] = "VK_NUMPAD3",
        [0x64] = "VK_NUMPAD4",
        [0x65] = "VK_NUMPAD5",
        [0x66] = "VK_NUMPAD6",
        [0x67] = "VK_NUMPAD7",
        [0x68] = "VK_NUMPAD8",
        [0x69] = "VK_NUMPAD9",
        [0x6A] = "VK_MULTIPLY",
        [0x6B] = "VK_ADD",
        [0x6D] = "VK_SUBTRACT",
        [0x6E] = "VK_DECIMAL",
        [0x6F] = "VK_DIVIDE",
        [0xA0] = "VK_LSHIFT",
        [0xA1] = "VK_RSHIFT",
        [0xA2] = "VK_LCONTROL",
        [0xA3] = "VK_RCONTROL",
        [0xA4] = "VK_LMENU",
        [0xA5] = "VK_RMENU",
        [0xA6] = "VK_BROWSER_BACK",
        [0xA7] = "VK_BROWSER_FORWARD",
        [0xA8] = "VK_BROWSER_REFRESH",
        [0xA9] = "VK_BROWSER_STOP",
        [0xAA] = "VK_BROWSER_SEARCH",
        [0xAB] = "VK_BROWSER_FAVORITES",
        [0xAC] = "VK_BROWSER_HOME",
        [0xAD] = "VK_VOLUME_MUTE",
        [0xAE] = "VK_VOLUME_DOWN",
        [0xAF] = "VK_VOLUME_UP",
        [0xB0] = "VK_MEDIA_NEXT_TRACK",
        [0xB1] = "VK_MEDIA_PREV_TRACK",
        [0xB2] = "VK_MEDIA_STOP",
        [0xB3] = "VK_MEDIA_PLAY_PAUSE",
        [0xB4] = "VK_LAUNCH_MAIL",
        [0xB5] = "VK_LAUNCH_MEDIA_SELECT",
        [0xB6] = "VK_LAUNCH_APP1",
        [0xB7] = "VK_LAUNCH_APP2"
    };

    public static string Format(uint virtualKey)
    {
        if (virtualKey == 0)
        {
            return "Не назначена";
        }

        if (VirtualKeyNames.TryGetValue(virtualKey, out var name))
        {
            return name;
        }

        if (virtualKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return $"F{virtualKey - 0x6F}";
        }

        return $"0x{virtualKey:X2}";
    }
}
