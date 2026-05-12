using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SpotifyFavoritesTool;

internal static class NativeMethods
{
    public const int WmHotkey = 0x0312;
    public const int WmAppCommand = 0x0319;
    public const int WmKeyDown = 0x0100;
    public const int WmKeyUp = 0x0101;
    public const int WmSysKeyDown = 0x0104;
    public const int WmSysKeyUp = 0x0105;
    public const int HotkeyToggleFavorite = 1001;
    public const int HotkeyShowFavoriteStatus = 1002;

    public static readonly uint ShowExistingWindowMessage = RegisterWindowMessage("SpotifyFavoritesTool.ShowExistingWindow");

    private const int WhKeyboardLl = 13;

    private const uint SwpNomove = 0x0002;
    private const uint SwpNosize = 0x0001;
    private const uint SwpNoactivate = 0x0010;
    private const uint SwpShowwindow = 0x0040;
    private const int SwShownormal = 1;

    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndBroadcast = new(0xffff);

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SendMessageW", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "FindWindowW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPosNative(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    public static void SignalExistingInstance()
    {
        if (ShowExistingWindowMessage != 0)
        {
            PostMessage(HwndBroadcast, ShowExistingWindowMessage, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static void BringWindowToFront(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(hWnd, SwShownormal);
        SetForegroundWindow(hWnd);
    }

    public static void ForceTopmost(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        SetWindowPosNative(hWnd, HwndTopmost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpNoactivate | SwpShowwindow);
    }

    public static IntPtr SetLowLevelKeyboardHook(LowLevelKeyboardProc callback)
    {
        using var currentProcess = Process.GetCurrentProcess();
        var moduleName = currentProcess.MainModule?.ModuleName;
        var moduleHandle = string.IsNullOrWhiteSpace(moduleName) ? IntPtr.Zero : GetModuleHandle(moduleName);
        return SetWindowsHookEx(WhKeyboardLl, callback, moduleHandle, 0);
    }

    public static void SendAppCommand(IntPtr hWnd, int appCommand)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        SendMessage(hWnd, WmAppCommand, hWnd, new IntPtr(appCommand << 16));
    }

    public static void PostShellAppCommand(int appCommand)
    {
        var shell = FindWindow("Shell_TrayWnd", null);
        if (shell == IntPtr.Zero)
        {
            return;
        }

        PostMessage(shell, WmAppCommand, IntPtr.Zero, new IntPtr(appCommand << 16));
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct KeyboardHookInfo
    {
        public readonly uint VirtualKey;
        public readonly uint ScanCode;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly IntPtr ExtraInfo;
    }
}
