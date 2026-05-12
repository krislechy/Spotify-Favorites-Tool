namespace SpotifyFavoritesTool;

public sealed class HotkeyManager
{
    private IntPtr _windowHandle;
    private bool _favoriteRegistered;
    private bool _favoriteRegistrationFailed;
    private bool _statusRegistered;
    private bool _statusRegistrationFailed;

    public void Register(IntPtr windowHandle, AppSettings settings)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        _windowHandle = windowHandle;
        Unregister();
        RegisterFavoriteHotkey(settings.LikeHotkeyVirtualKey, settings.KeepMediaKeysLocalDuringRdp);
        RegisterStatusHotkey(settings.FavoriteStatusHotkeyVirtualKey, settings.KeepMediaKeysLocalDuringRdp);
    }

    public void Unregister()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        if (_favoriteRegistered)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotkeyToggleFavorite);
        }

        if (_statusRegistered)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotkeyShowFavoriteStatus);
        }

        _favoriteRegistered = false;
        _favoriteRegistrationFailed = false;
        _statusRegistered = false;
        _statusRegistrationFailed = false;
    }

    public bool TryGetAction(IntPtr hotkeyId, out HotkeyAction action)
    {
        action = hotkeyId.ToInt32() switch
        {
            NativeMethods.HotkeyToggleFavorite => HotkeyAction.ToggleFavorite,
            NativeMethods.HotkeyShowFavoriteStatus => HotkeyAction.ShowFavoriteStatus,
            _ => HotkeyAction.None
        };

        return action != HotkeyAction.None;
    }

    public string GetRegistrationMessage()
    {
        var parts = new List<string>();
        if (_favoriteRegistrationFailed)
        {
            parts.Add("клавиша Избранного занята или недоступна");
        }

        if (_statusRegistrationFailed)
        {
            parts.Add("клавиша статуса занята или недоступна");
        }

        return parts.Count == 0 ? string.Empty : $" {string.Join("; ", parts)}.";
    }

    private void RegisterFavoriteHotkey(uint virtualKey, bool mediaKeysHandledByInterceptor)
    {
        if (virtualKey == 0)
        {
            return;
        }

        if (mediaKeysHandledByInterceptor && MediaKeyInterceptor.IsMediaKey(virtualKey))
        {
            return;
        }

        _favoriteRegistered = NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.HotkeyToggleFavorite,
            0,
            virtualKey);
        _favoriteRegistrationFailed = !_favoriteRegistered;
    }

    private void RegisterStatusHotkey(uint virtualKey, bool mediaKeysHandledByInterceptor)
    {
        if (virtualKey == 0)
        {
            return;
        }

        if (mediaKeysHandledByInterceptor && MediaKeyInterceptor.IsMediaKey(virtualKey))
        {
            return;
        }

        _statusRegistered = NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.HotkeyShowFavoriteStatus,
            0,
            virtualKey);
        _statusRegistrationFailed = !_statusRegistered;
    }
}

public enum HotkeyAction
{
    None,
    ToggleFavorite,
    ShowFavoriteStatus
}
