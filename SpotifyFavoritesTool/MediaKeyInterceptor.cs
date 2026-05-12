using System.Runtime.InteropServices;

namespace SpotifyFavoritesTool;

public sealed class MediaKeyInterceptor : IDisposable
{
    private const int HotkeyIdBase = 3100;

    private const uint VkVolumeMute = 0xAD;
    private const uint VkVolumeDown = 0xAE;
    private const uint VkVolumeUp = 0xAF;
    private const uint VkMediaNextTrack = 0xB0;
    private const uint VkMediaPreviousTrack = 0xB1;
    private const uint VkMediaStop = 0xB2;
    private const uint VkMediaPlayPause = 0xB3;
    private const uint VkLaunchMediaSelect = 0xB5;

    private const int AppCommandVolumeMute = 8;
    private const int AppCommandVolumeDown = 9;
    private const int AppCommandVolumeUp = 10;
    private const int AppCommandMediaNextTrack = 11;
    private const int AppCommandMediaPreviousTrack = 12;
    private const int AppCommandMediaStop = 13;
    private const int AppCommandMediaPlayPause = 14;
    private const int AppCommandLaunchMediaSelect = 16;

    private static readonly IReadOnlyDictionary<uint, int> AppCommands = new Dictionary<uint, int>
    {
        [VkVolumeMute] = AppCommandVolumeMute,
        [VkVolumeDown] = AppCommandVolumeDown,
        [VkVolumeUp] = AppCommandVolumeUp,
        [VkMediaNextTrack] = AppCommandMediaNextTrack,
        [VkMediaPreviousTrack] = AppCommandMediaPreviousTrack,
        [VkMediaStop] = AppCommandMediaStop,
        [VkMediaPlayPause] = AppCommandMediaPlayPause,
        [VkLaunchMediaSelect] = AppCommandLaunchMediaSelect
    };

    private readonly NativeMethods.LowLevelKeyboardProc _hookCallback;
    private readonly HashSet<uint> _pressedKeys = [];
    private readonly HashSet<uint> _applicationHotkeys = [];
    private readonly Dictionary<int, uint> _registeredHotkeys = [];
    private readonly Dictionary<uint, DateTimeOffset> _lastHandledAt = [];
    private readonly Dictionary<uint, int> _failedHotkeys = [];

    private IntPtr _hookHandle;
    private IntPtr _targetWindowHandle;
    private bool _disposed;

    public MediaKeyInterceptor()
    {
        _hookCallback = HookCallback;
    }

    public event EventHandler<MediaKeyPressedEventArgs>? MediaKeyPressed;

    public bool IsEnabled => _hookHandle != IntPtr.Zero || _registeredHotkeys.Count > 0;
    public int LastInstallError { get; private set; }
    public bool ShouldRetryRegistration => _applicationHotkeys.Any(virtualKey => !_registeredHotkeys.ContainsValue(virtualKey));
    public string RegistrationSummary => CreateRegistrationSummary();

    public bool Apply(bool enabled, IntPtr targetWindowHandle, IEnumerable<uint> applicationHotkeys)
    {
        if (_disposed)
        {
            return false;
        }

        _targetWindowHandle = targetWindowHandle;
        _applicationHotkeys.Clear();
        _failedHotkeys.Clear();
        foreach (var virtualKey in applicationHotkeys.Where(IsMediaKey))
        {
            _applicationHotkeys.Add(virtualKey);
        }

        if (!enabled || targetWindowHandle == IntPtr.Zero)
        {
            Disable();
            return true;
        }

        RegisterMediaHotkeys(targetWindowHandle);

        if (_hookHandle != IntPtr.Zero)
        {
            return true;
        }

        _hookHandle = NativeMethods.SetLowLevelKeyboardHook(_hookCallback);
        LastInstallError = _hookHandle == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
        return IsEnabled;
    }

    public void Disable()
    {
        _pressedKeys.Clear();
        _lastHandledAt.Clear();
        _failedHotkeys.Clear();
        UnregisterMediaHotkeys();
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Disable();
        _disposed = true;
    }

    public bool TryHandleHotkey(IntPtr hotkeyId)
    {
        return _registeredHotkeys.TryGetValue(hotkeyId.ToInt32(), out var virtualKey)
            && HandleMediaKeyDown(virtualKey);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var isKeyDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
        var isKeyUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;
        if (!isKeyDown && !isKeyUp)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var info = Marshal.PtrToStructure<NativeMethods.KeyboardHookInfo>(lParam);
        if (!IsMediaKey(info.VirtualKey))
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        if (isKeyUp)
        {
            _pressedKeys.Remove(info.VirtualKey);
            return new IntPtr(1);
        }

        var isFirstPress = _pressedKeys.Add(info.VirtualKey);
        if (!isFirstPress && (!CanRepeat(info.VirtualKey) || _applicationHotkeys.Contains(info.VirtualKey)))
        {
            return new IntPtr(1);
        }

        HandleMediaKeyDown(info.VirtualKey);
        return new IntPtr(1);
    }

    private bool HandleMediaKeyDown(uint virtualKey)
    {
        if (IsDuplicatePress(virtualKey))
        {
            return true;
        }

        var args = new MediaKeyPressedEventArgs(virtualKey);
        MediaKeyPressed?.Invoke(this, args);
        if (!args.Handled && AppCommands.TryGetValue(virtualKey, out var appCommand))
        {
            NativeMethods.PostShellAppCommand(appCommand);
        }

        return true;
    }

    private bool IsDuplicatePress(uint virtualKey)
    {
        if (CanRepeat(virtualKey))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (_lastHandledAt.TryGetValue(virtualKey, out var lastHandledAt)
            && now - lastHandledAt < TimeSpan.FromMilliseconds(350))
        {
            return true;
        }

        _lastHandledAt[virtualKey] = now;
        return false;
    }

    public static bool IsMediaKey(uint virtualKey)
    {
        return AppCommands.ContainsKey(virtualKey);
    }

    private static bool CanRepeat(uint virtualKey)
    {
        return virtualKey is VkVolumeDown or VkVolumeUp;
    }

    private void RegisterMediaHotkeys(IntPtr targetWindowHandle)
    {
        UnregisterMediaHotkeys();
        _failedHotkeys.Clear();

        var index = 0;
        foreach (var virtualKey in GetRegistrationOrder())
        {
            var id = HotkeyIdBase + index++;
            if (NativeMethods.RegisterHotKey(targetWindowHandle, id, 0, virtualKey))
            {
                _registeredHotkeys[id] = virtualKey;
                continue;
            }

            _failedHotkeys[virtualKey] = Marshal.GetLastWin32Error();
        }
    }

    private void UnregisterMediaHotkeys()
    {
        if (_targetWindowHandle == IntPtr.Zero || _registeredHotkeys.Count == 0)
        {
            _registeredHotkeys.Clear();
            return;
        }

        foreach (var id in _registeredHotkeys.Keys)
        {
            NativeMethods.UnregisterHotKey(_targetWindowHandle, id);
        }

        _registeredHotkeys.Clear();
    }

    private IEnumerable<uint> GetRegistrationOrder()
    {
        foreach (var virtualKey in _applicationHotkeys)
        {
            yield return virtualKey;
        }

        foreach (var virtualKey in AppCommands.Keys)
        {
            if (!_applicationHotkeys.Contains(virtualKey))
            {
                yield return virtualKey;
            }
        }
    }

    private string CreateRegistrationSummary()
    {
        if (_targetWindowHandle == IntPtr.Zero)
        {
            return "RDP media keys: no window handle.";
        }

        var registered = _registeredHotkeys.Values
            .Distinct()
            .Select(HotkeyFormatter.Format)
            .OrderBy(static value => value)
            .ToArray();

        var failedApplicationHotkeys = _applicationHotkeys
            .Where(virtualKey => !_registeredHotkeys.ContainsValue(virtualKey))
            .Select(virtualKey => _failedHotkeys.TryGetValue(virtualKey, out var error)
                ? $"{HotkeyFormatter.Format(virtualKey)} Win32 {error}"
                : $"{HotkeyFormatter.Format(virtualKey)} not registered")
            .ToArray();

        var registeredText = registered.Length == 0 ? "none" : string.Join(", ", registered);
        if (failedApplicationHotkeys.Length == 0)
        {
            return $"RDP media keys captured: {registeredText}.";
        }

        return $"RDP media keys captured: {registeredText}. Failed app hotkeys: {string.Join(", ", failedApplicationHotkeys)}.";
    }
}

public sealed class MediaKeyPressedEventArgs : EventArgs
{
    public MediaKeyPressedEventArgs(uint virtualKey)
    {
        VirtualKey = virtualKey;
    }

    public uint VirtualKey { get; }
    public bool Handled { get; set; }
}
