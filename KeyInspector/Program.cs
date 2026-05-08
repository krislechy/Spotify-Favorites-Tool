using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyInspector;

internal static class Program
{
    private const int WhKeyboardLl = 13;
    private const int WmInput = 0x00FF;
    private const int WmClose = 0x0010;
    private const int WmDestroy = 0x0002;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeydown = 0x0104;
    private const int WmSyskeyup = 0x0105;
    private const int RidInput = 0x10000003;
    private const int RidiDeviceName = 0x20000007;
    private const int RimTypeKeyboard = 1;
    private const int RimTypeHid = 2;
    private const uint RidevInputSink = 0x00000100;
    private const uint MapvkVkToVscEx = 4;
    private const ushort RiKeyBreak = 0x0001;
    private const ushort RiKeyE0 = 0x0002;
    private const ushort RiKeyE1 = 0x0004;

    private static readonly WndProcDelegate WindowProc = WndProc;
    private static readonly LowLevelKeyboardProc KeyboardProc = KeyboardHookCallback;
    private static readonly Dictionary<IntPtr, string> DeviceNames = new();
    private static IntPtr _keyboardHook;
    private static IntPtr _windowHandle;

    [STAThread]
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "Key Inspector";
        Console.WriteLine("Key Inspector для Spotify Relay Overlay");
        Console.WriteLine("Нажимай любые клавиши, включая медиа-кнопки. Для выхода нажми Ctrl+C.");
        Console.WriteLine("Если часть клавиш не видна, запусти консоль от администратора.");
        Console.WriteLine();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            if (_windowHandle != IntPtr.Zero)
            {
                PostMessage(_windowHandle, WmClose, IntPtr.Zero, IntPtr.Zero);
            }
        };

        _windowHandle = CreateHiddenWindow();
        RegisterRawInput(_windowHandle);
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, KeyboardProc, GetModuleHandle(null), 0);
        if (_keyboardHook == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось поставить keyboard hook.");
        }

        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
        }
    }

    private static IntPtr CreateHiddenWindow()
    {
        var instance = GetModuleHandle(null);
        var windowClass = new WndClassEx
        {
            Size = (uint)Marshal.SizeOf<WndClassEx>(),
            WindowProc = WindowProc,
            Instance = instance,
            ClassName = "SpotifyRelayOverlay.KeyInspectorWindow"
        };

        var atom = RegisterClassEx(ref windowClass);
        if (atom == 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1410)
            {
                throw new Win32Exception(error, "Не удалось зарегистрировать hidden window class.");
            }
        }

        var handle = CreateWindowEx(
            0,
            windowClass.ClassName,
            "Key Inspector Hidden Window",
            0,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);

        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось создать hidden window.");
        }

        return handle;
    }

    private static void RegisterRawInput(IntPtr targetWindow)
    {
        var devices = new[]
        {
            // Generic desktop keyboard.
            new RawInputDevice(0x01, 0x06, RidevInputSink, targetWindow),
            // Consumer control: media keys, volume keys, browser keys.
            new RawInputDevice(0x0C, 0x01, RidevInputSink, targetWindow)
        };

        if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось зарегистрировать raw input.");
        }
    }

    private static IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WmInput:
                PrintRawInput(lParam);
                return IntPtr.Zero;
            case WmClose:
                DestroyWindow(hwnd);
                return IntPtr.Zero;
            case WmDestroy:
                PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private static IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            if (message is WmKeydown or WmKeyup or WmSyskeydown or WmSyskeyup)
            {
                var data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
                PrintKeyboardHook(message, data);
            }
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private static void PrintKeyboardHook(int message, KeyboardHookData data)
    {
        var isDown = message is WmKeydown or WmSyskeydown;
        var isSystem = message is WmSyskeydown or WmSyskeyup;
        var isExtended = (data.Flags & KeyboardHookFlags.Extended) != 0;
        var isInjected = (data.Flags & KeyboardHookFlags.Injected) != 0;
        var name = GetKeyName(data.VkCode, data.ScanCode, isExtended);

        PrintLine(
            "HOOK",
            isDown ? "DOWN" : "UP  ",
            $"VK=0x{data.VkCode:X2} {DescribeVirtualKey(data.VkCode),-28} SC=0x{data.ScanCode:X3} EXT={YesNo(isExtended)} SYS={YesNo(isSystem)} INJECTED={YesNo(isInjected)} NAME=\"{name}\"");
    }

    private static void PrintRawInput(IntPtr rawInputHandle)
    {
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        var size = 0u;
        var result = GetRawInputData(rawInputHandle, RidInput, IntPtr.Zero, ref size, headerSize);
        if (result != 0 || size == 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            result = GetRawInputData(rawInputHandle, RidInput, buffer, ref size, headerSize);
            if (result == 0xFFFFFFFF)
            {
                return;
            }

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            var dataPointer = IntPtr.Add(buffer, Marshal.SizeOf<RawInputHeader>());

            if (header.Type == RimTypeKeyboard)
            {
                var keyboard = Marshal.PtrToStructure<RawKeyboard>(dataPointer);
                PrintRawKeyboard(header, keyboard);
            }
            else if (header.Type == RimTypeHid)
            {
                var hid = Marshal.PtrToStructure<RawHid>(dataPointer);
                PrintRawHid(header, dataPointer, hid);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void PrintRawKeyboard(RawInputHeader header, RawKeyboard keyboard)
    {
        var isUp = (keyboard.Flags & RiKeyBreak) != 0;
        var isExtended = (keyboard.Flags & (RiKeyE0 | RiKeyE1)) != 0;
        var name = GetKeyName(keyboard.VKey, keyboard.MakeCode, isExtended);

        PrintLine(
            "RAWK",
            isUp ? "UP  " : "DOWN",
            $"VK=0x{keyboard.VKey:X2} {DescribeVirtualKey(keyboard.VKey),-28} MAKE=0x{keyboard.MakeCode:X3} FLAGS=0x{keyboard.Flags:X4} MSG=0x{keyboard.Message:X4} DEVICE=\"{GetDeviceName(header.Device)}\" NAME=\"{name}\"");
    }

    private static void PrintRawHid(RawInputHeader header, IntPtr dataPointer, RawHid hid)
    {
        var dataLength = checked((int)(hid.SizeHid * hid.Count));
        if (dataLength <= 0)
        {
            return;
        }

        var data = new byte[dataLength];
        Marshal.Copy(IntPtr.Add(dataPointer, Marshal.SizeOf<RawHid>()), data, 0, data.Length);
        var usages = GuessConsumerUsages(data);
        var usageText = usages.Count == 0 ? string.Empty : $" USAGE={string.Join(", ", usages)}";

        PrintLine(
            "RAWH",
            "HID ",
            $"SIZE={hid.SizeHid} COUNT={hid.Count} BYTES={Convert.ToHexString(data)}{usageText} DEVICE=\"{GetDeviceName(header.Device)}\"");
    }

    private static List<string> GuessConsumerUsages(byte[] data)
    {
        var usages = new List<string>();
        AddUsageCandidate(data, 0, usages);
        AddUsageCandidate(data, 1, usages);
        return usages;
    }

    private static void AddUsageCandidate(byte[] data, int offset, List<string> usages)
    {
        if (data.Length < offset + 2)
        {
            return;
        }

        var usage = (ushort)(data[offset] | (data[offset + 1] << 8));
        if (usage == 0)
        {
            return;
        }

        var text = ConsumerUsageNames.TryGetValue(usage, out var name)
            ? $"0x{usage:X4} {name}"
            : $"0x{usage:X4}";
        if (!usages.Contains(text))
        {
            usages.Add(text);
        }
    }

    private static string GetDeviceName(IntPtr device)
    {
        if (device == IntPtr.Zero)
        {
            return "none";
        }

        if (DeviceNames.TryGetValue(device, out var cached))
        {
            return cached;
        }

        var size = 0u;
        GetRawInputDeviceInfo(device, RidiDeviceName, IntPtr.Zero, ref size);
        if (size == 0)
        {
            DeviceNames[device] = $"0x{device.ToInt64():X}";
            return DeviceNames[device];
        }

        var buffer = Marshal.AllocHGlobal(checked((int)size * 2));
        try
        {
            if (GetRawInputDeviceInfo(device, RidiDeviceName, buffer, ref size) == 0xFFFFFFFF)
            {
                DeviceNames[device] = $"0x{device.ToInt64():X}";
            }
            else
            {
                DeviceNames[device] = Marshal.PtrToStringUni(buffer) ?? $"0x{device.ToInt64():X}";
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return DeviceNames[device];
    }

    private static string GetKeyName(uint vk, uint scanCode, bool isExtended)
    {
        var scan = scanCode != 0 ? scanCode : MapVirtualKey(vk, MapvkVkToVscEx);
        var lParam = (int)(scan << 16);
        if (isExtended)
        {
            lParam |= 1 << 24;
        }

        var builder = new StringBuilder(128);
        var length = GetKeyNameText(lParam, builder, builder.Capacity);
        if (length > 0)
        {
            return builder.ToString();
        }

        return VirtualKeyNames.TryGetValue(vk, out var name) ? name : "unknown";
    }

    private static string DescribeVirtualKey(uint vk)
    {
        return VirtualKeyNames.TryGetValue(vk, out var name) ? name : "VK_UNKNOWN";
    }

    private static void PrintLine(string source, string action, string details)
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {source} {action} {details}");
    }

    private static string YesNo(bool value)
    {
        return value ? "Y" : "N";
    }

    private static readonly Dictionary<uint, string> VirtualKeyNames = new()
    {
        [0x01] = "VK_LBUTTON",
        [0x02] = "VK_RBUTTON",
        [0x03] = "VK_CANCEL",
        [0x04] = "VK_MBUTTON",
        [0x05] = "VK_XBUTTON1",
        [0x06] = "VK_XBUTTON2",
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
        [0x5D] = "VK_APPS",
        [0x5F] = "VK_SLEEP",
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
        [0x70] = "VK_F1",
        [0x71] = "VK_F2",
        [0x72] = "VK_F3",
        [0x73] = "VK_F4",
        [0x74] = "VK_F5",
        [0x75] = "VK_F6",
        [0x76] = "VK_F7",
        [0x77] = "VK_F8",
        [0x78] = "VK_F9",
        [0x79] = "VK_F10",
        [0x7A] = "VK_F11",
        [0x7B] = "VK_F12",
        [0x90] = "VK_NUMLOCK",
        [0x91] = "VK_SCROLL",
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
        [0xB7] = "VK_LAUNCH_APP2",
        [0xBA] = "VK_OEM_1",
        [0xBB] = "VK_OEM_PLUS",
        [0xBC] = "VK_OEM_COMMA",
        [0xBD] = "VK_OEM_MINUS",
        [0xBE] = "VK_OEM_PERIOD",
        [0xBF] = "VK_OEM_2",
        [0xC0] = "VK_OEM_3",
        [0xDB] = "VK_OEM_4",
        [0xDC] = "VK_OEM_5",
        [0xDD] = "VK_OEM_6",
        [0xDE] = "VK_OEM_7"
    };

    private static readonly Dictionary<ushort, string> ConsumerUsageNames = new()
    {
        [0x00B0] = "Play",
        [0x00B1] = "Pause",
        [0x00B2] = "Record",
        [0x00B3] = "Fast Forward",
        [0x00B4] = "Rewind",
        [0x00B5] = "Scan Next Track",
        [0x00B6] = "Scan Previous Track",
        [0x00B7] = "Stop",
        [0x00CD] = "Play/Pause",
        [0x00E2] = "Mute",
        [0x00E9] = "Volume Up",
        [0x00EA] = "Volume Down",
        [0x0183] = "AL Consumer Control Configuration",
        [0x018A] = "AL Email Reader",
        [0x0192] = "AL Calculator",
        [0x0194] = "AL Local Machine Browser",
        [0x0221] = "AC Search",
        [0x0223] = "AC Home",
        [0x0224] = "AC Back",
        [0x0225] = "AC Forward",
        [0x0226] = "AC Stop",
        [0x0227] = "AC Refresh",
        [0x022A] = "AC Bookmarks"
    };

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [Flags]
    private enum KeyboardHookFlags : uint
    {
        Extended = 0x01,
        Injected = 0x10,
        AltDown = 0x20,
        Up = 0x80
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public WndProcDelegate WindowProc;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr IconSmall;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RawInputDevice
    {
        public RawInputDevice(ushort usagePage, ushort usage, uint flags, IntPtr target)
        {
            UsagePage = usagePage;
            Usage = usage;
            Flags = flags;
            Target = target;
        }

        public readonly ushort UsagePage;
        public readonly ushort Usage;
        public readonly uint Flags;
        public readonly IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawHid
    {
        public uint SizeHid;
        public uint Count;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookData
    {
        public uint VkCode;
        public uint ScanCode;
        public KeyboardHookFlags Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Window;
        public uint Value;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WndClassEx windowClass);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    private static extern int GetMessage(out Message message, IntPtr hwnd, uint minMessage, uint maxMessage);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static extern IntPtr DispatchMessage(ref Message message);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        RawInputDevice[] rawInputDevices,
        uint numberDevices,
        uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        int command,
        IntPtr data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(IntPtr device, int command, IntPtr data, ref uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelKeyboardProc hookProc, IntPtr instance, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("user32.dll", EntryPoint = "GetKeyNameTextW", CharSet = CharSet.Unicode)]
    private static extern int GetKeyNameText(int lParam, StringBuilder keyName, int size);
}
