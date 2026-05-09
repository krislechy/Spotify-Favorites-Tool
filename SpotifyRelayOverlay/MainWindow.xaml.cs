using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace SpotifyRelayOverlay;

public partial class MainWindow : Window
{
    private static readonly TimeSpan NormalPollInterval = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan NearTrackEndPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TrackEndLookahead = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan InitialRateLimitBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxRateLimitBackoff = TimeSpan.FromMinutes(5);

    private readonly SettingsStore _settings = new();
    private readonly SpotifyAuthService _auth;
    private readonly SpotifyClient _spotify;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _topmostTimer;

    private PlaybackSnapshot? _current;
    private SettingsWindow? _settingsWindow;
    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _isPolling;
    private bool _isRunningPlaybackCommand;
    private bool _isTogglingLike;
    private bool _hotkeysRegistered;
    private bool _isExiting;
    private bool _hasSeenPlayback;
    private TimeSpan _rateLimitBackoff = TimeSpan.Zero;
    private DateTimeOffset _apiPausedUntil = DateTimeOffset.MinValue;
    private string? _lastTrackId;
    private Forms.NotifyIcon? _trayIcon;
    private ToastWindow? _toastWindow;

    public MainWindow()
    {
        InitializeComponent();

        _auth = new SpotifyAuthService(_settings);
        _spotify = new SpotifyClient(_auth);

        _pollTimer = new DispatcherTimer { Interval = NormalPollInterval };
        _pollTimer.Tick += async (_, _) => await RefreshPlaybackAsync();

        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _topmostTimer.Tick += (_, _) => KeepAboveWindows();

        InitializeTrayIcon();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowPosition();
        RenderEmpty("Spotify Relay", "Открой настройки и подключи Spotify.", "Не подключено");
        _pollTimer.Start();
        ApplyModeSettings();
        await RefreshPlaybackAsync(allowAutomaticTrackToast: false);
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        ApplyModeSettings();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        SaveWindowPosition();
        _pollTimer.Stop();
        _topmostTimer.Stop();
        UnregisterHotkeys();
        _source?.RemoveHook(WndProc);
        _trayIcon?.Dispose();
        _toastWindow?.Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !IsInsideButton(e.OriginalSource as DependencyObject))
        {
            DragMove();
        }
    }

    private async void LikeButton_Click(object sender, RoutedEventArgs e)
    {
        await ToggleLikeAsync();
    }

    private async void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        await RunTrackSwitchCommandAsync(() => _spotify.PreviousTrackAsync(), "Предыдущий трек");
    }

    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = _current;
        if (snapshot is null)
        {
            return;
        }

        await RunPlaybackCommandAsync(() => _spotify.TogglePlaybackAsync(snapshot.IsPlaying));
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        await RunTrackSwitchCommandAsync(() => _spotify.NextTrackAsync(), "Следующий трек");
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsWindow();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void InitializeTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => Dispatcher.Invoke(BringMainWindowToFront));
        menu.Items.Add("Настройки", null, (_, _) => Dispatcher.Invoke(ShowSettingsWindow));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Spotify Relay Overlay",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(BringMainWindowToFront);
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
                ?? System.Drawing.SystemIcons.Application;
        }
        catch
        {
            return System.Drawing.SystemIcons.Application;
        }
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    private void ShowSettingsWindow()
    {
        BringMainWindowToFront();

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, _auth)
        {
            Owner = this,
            Topmost = _settings.Current.OverlayEnabled && !_settings.Current.SafeMode
        };
        _settingsWindow.AuthChanged += async (_, _) => await RefreshPlaybackAsync(allowAutomaticTrackToast: false);
        _settingsWindow.SettingsChanged += (_, _) =>
        {
            ApplyModeSettings();
            if (_settingsWindow is not null)
            {
                _settingsWindow.Topmost = _settings.Current.OverlayEnabled && !_settings.Current.SafeMode;
            }
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private async Task RefreshPlaybackAsync(bool allowAutomaticTrackToast = true, bool force = false)
    {
        if (_isPolling || (!force && _isRunningPlaybackCommand))
        {
            return;
        }

        _isPolling = true;
        try
        {
            if (!_auth.HasClientId)
            {
                RenderEmpty("Нужен Spotify Client ID", "Открой настройки и вставь Client ID.", "Не настроено");
                return;
            }

            if (!_auth.HasAnyToken)
            {
                RenderEmpty("Spotify не подключен", "Открой настройки и войди в аккаунт.", "Ожидает входа");
                return;
            }

            if (IsApiBackoffActive())
            {
                return;
            }

            var snapshot = await _spotify.GetPlaybackAsync();
            _current = snapshot;
            _rateLimitBackoff = TimeSpan.Zero;
            _apiPausedUntil = DateTimeOffset.MinValue;
            UpdatePollingInterval(snapshot);
            RenderPlayback(snapshot);
            TrackAutomaticChange(snapshot, allowAutomaticTrackToast);
        }
        catch (SpotifyRateLimitException ex)
        {
            HandleRateLimit(ex);
        }
        catch (Exception ex)
        {
            RenderEmpty("Ошибка Spotify", Shorten(ex.Message, 88), "Ошибка");
            SetPollingInterval(IdlePollInterval);
        }
        finally
        {
            _isPolling = false;
        }
    }

    private async Task ToggleLikeAsync()
    {
        if (_isTogglingLike)
        {
            return;
        }

        _isTogglingLike = true;
        try
        {
            if (IsApiBackoffActive())
            {
                return;
            }

            LikeButton.IsEnabled = false;

            var snapshot = _current;
            if (snapshot?.Track is null)
            {
                return;
            }

            var isLiked = await _spotify.ToggleLikeAsync(snapshot.Track, snapshot.IsLiked);
            _current = snapshot with { IsLiked = isLiked };
            RenderPlayback(_current);
            TrackAutomaticChange(_current, allowAutomaticTrackToast: false);

            ShowToastIfEnabled(
                ToastKind.Like,
                _current,
                isLiked ? "Лайкнуто" : "Лайк убран",
                isLiked ? "Добавлено в избранное" : "Убрано из избранного");
        }
        catch (SpotifyRateLimitException ex)
        {
            HandleRateLimit(ex);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Лайк не изменен";
            LikedText.Text = Shorten(ex.Message, 72);
        }
        finally
        {
            LikeButton.IsEnabled = _current?.Track is not null;
            _isTogglingLike = false;
        }
    }

    private async Task RunTrackSwitchCommandAsync(Func<Task> command, string toastAction)
    {
        if (_isRunningPlaybackCommand)
        {
            return;
        }

        _isRunningPlaybackCommand = true;
        try
        {
            if (IsApiBackoffActive())
            {
                return;
            }

            SetPlaybackButtonsEnabled(false);
            await command();
            await Task.Delay(750);
            await RefreshPlaybackAsync(allowAutomaticTrackToast: false, force: true);
            if (_current is not null)
            {
                ShowToastIfEnabled(ToastKind.ManualTrack, _current, toastAction);
            }
        }
        catch (SpotifyRateLimitException ex)
        {
            HandleRateLimit(ex);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Команда не выполнена";
            LikedText.Text = Shorten(ex.Message, 84);
        }
        finally
        {
            SetPlaybackButtonsEnabled(_current?.Track is not null);
            _isRunningPlaybackCommand = false;
        }
    }

    private async Task RunPlaybackCommandAsync(Func<Task> command)
    {
        if (_isRunningPlaybackCommand)
        {
            return;
        }

        _isRunningPlaybackCommand = true;
        try
        {
            if (IsApiBackoffActive())
            {
                return;
            }

            SetPlaybackButtonsEnabled(false);
            await command();
            await Task.Delay(750);
            await RefreshPlaybackAsync(allowAutomaticTrackToast: false, force: true);
        }
        catch (SpotifyRateLimitException ex)
        {
            HandleRateLimit(ex);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Команда не выполнена";
            LikedText.Text = Shorten(ex.Message, 84);
        }
        finally
        {
            SetPlaybackButtonsEnabled(_current?.Track is not null);
            _isRunningPlaybackCommand = false;
        }
    }

    private void HandleRateLimit(SpotifyRateLimitException ex)
    {
        _rateLimitBackoff = _rateLimitBackoff == TimeSpan.Zero
            ? InitialRateLimitBackoff
            : TimeSpan.FromSeconds(Math.Min(MaxRateLimitBackoff.TotalSeconds, _rateLimitBackoff.TotalSeconds * 2));

        _apiPausedUntil = DateTimeOffset.UtcNow.Add(_rateLimitBackoff);
        SetPollingInterval(_rateLimitBackoff);
        ShowApiPausedStatus(ex.Message, _rateLimitBackoff);
    }

    private bool IsApiBackoffActive()
    {
        var remaining = _apiPausedUntil - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return false;
        }

        ShowApiPausedStatus("Spotify временно ограничил запросы.", remaining);
        return true;
    }

    private void ShowApiPausedStatus(string message, TimeSpan remaining)
    {
        if (_current?.Track is null)
        {
            RenderEmpty("Spotify ограничил запросы", $"Повтор через {FormatDelay(remaining)}.", "Пауза API");
            return;
        }

        StatusText.Text = $"{message} Повтор через {FormatDelay(remaining)}.";
    }

    private void UpdatePollingInterval(PlaybackSnapshot snapshot)
    {
        if (snapshot.Track is null || !snapshot.IsPlaying)
        {
            SetPollingInterval(IdlePollInterval);
            return;
        }

        var remainingMs = snapshot.DurationMs - snapshot.ProgressMs;
        var remaining = TimeSpan.FromMilliseconds(Math.Max(0, remainingMs));
        SetPollingInterval(remaining <= TrackEndLookahead ? NearTrackEndPollInterval : NormalPollInterval);
    }

    private void SetPollingInterval(TimeSpan interval)
    {
        if (_pollTimer.Interval != interval)
        {
            _pollTimer.Interval = interval;
        }
    }

    private static string FormatDelay(TimeSpan delay)
    {
        return delay >= TimeSpan.FromMinutes(1)
            ? $"{Math.Ceiling(delay.TotalMinutes):0} мин"
            : $"{Math.Ceiling(delay.TotalSeconds):0} сек";
    }

    private void TrackAutomaticChange(PlaybackSnapshot snapshot, bool allowAutomaticTrackToast)
    {
        var newTrackId = snapshot.Track?.Id;
        var hadPreviousTrack = _hasSeenPlayback && !string.IsNullOrWhiteSpace(_lastTrackId);
        var changedTrack = !string.IsNullOrWhiteSpace(newTrackId)
            && !string.Equals(_lastTrackId, newTrackId, StringComparison.Ordinal);

        _hasSeenPlayback = true;
        _lastTrackId = newTrackId;

        if (allowAutomaticTrackToast && hadPreviousTrack && changedTrack)
        {
            ShowToastIfEnabled(ToastKind.AutomaticTrack, snapshot, "Сейчас играет");
        }
    }

    private void RenderPlayback(PlaybackSnapshot snapshot)
    {
        if (snapshot.Track is null)
        {
            RenderEmpty("Нет трека", "Запусти трек в Spotify.", snapshot.Status);
            return;
        }

        SetAlbumArt(snapshot.Track.AlbumImageUrl);
        StatusText.Text = snapshot.Status;
        TrackTitle.Text = snapshot.Track.Name;
        ArtistText.Text = snapshot.Track.Artists;
        Progress.Value = snapshot.DurationMs > 0
            ? Math.Clamp(snapshot.ProgressMs * 100.0 / snapshot.DurationMs, 0, 100)
            : 0;
        LikedText.Text = snapshot.IsLiked ? "Лайкнуто" : "Не лайкнуто";
        LikeButton.Content = snapshot.IsLiked ? "\u2665" : "\u2661";
        LikeButton.ToolTip = snapshot.IsLiked ? "Убрать лайк" : "Лайкнуть текущий трек";
        LikeButton.IsEnabled = true;
        PlayPauseButton.Content = snapshot.IsPlaying ? "\u23F8" : "\u25B6";
        PlayPauseButton.ToolTip = snapshot.IsPlaying ? "Пауза" : "Плей";
        SetPlaybackButtonsEnabled(true);
    }

    private void RenderEmpty(string title, string subtitle, string status)
    {
        _current = null;
        AlbumArt.Source = null;
        AlbumPlaceholder.Visibility = Visibility.Visible;
        StatusText.Text = status;
        TrackTitle.Text = title;
        ArtistText.Text = subtitle;
        Progress.Value = 0;
        LikedText.Text = string.Empty;
        LikeButton.Content = "\u2661";
        LikeButton.IsEnabled = false;
        PlayPauseButton.Content = "\u25B6";
        PlayPauseButton.ToolTip = "Пауза / плей";
        SetPlaybackButtonsEnabled(false);
    }

    private void SetAlbumArt(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            AlbumArt.Source = null;
            AlbumPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(imageUrl, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            AlbumArt.Source = image;
            AlbumPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            AlbumArt.Source = null;
            AlbumPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void ApplyModeSettings()
    {
        UnregisterHotkeys();

        if (!IsVisible)
        {
            Show();
        }

        if (_settings.Current.OverlayEnabled && !_settings.Current.SafeMode)
        {
            Topmost = true;
            RegisterOverlayHotkeys();
            if (!_topmostTimer.IsEnabled)
            {
                _topmostTimer.Start();
            }

            KeepAboveWindows();
            return;
        }

        _topmostTimer.Stop();
        Topmost = false;

        if (!_settings.Current.OverlayEnabled)
        {
            RegisterBackgroundHotkey();
        }
    }

    private void RegisterOverlayHotkeys()
    {
        if (_hotkeysRegistered || _hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.RegisterHotKey(_hwnd, NativeMethods.HotkeyToggleLike, NativeMethods.ModControl | NativeMethods.ModAlt, (uint)KeyInterop.VirtualKeyFromKey(Key.L));
        NativeMethods.RegisterHotKey(_hwnd, NativeMethods.HotkeyToggleVisibility, NativeMethods.ModControl | NativeMethods.ModAlt, (uint)KeyInterop.VirtualKeyFromKey(Key.H));
        NativeMethods.RegisterHotKey(_hwnd, NativeMethods.HotkeyPreviousTrack, NativeMethods.ModControl | NativeMethods.ModAlt, (uint)KeyInterop.VirtualKeyFromKey(Key.Left));
        NativeMethods.RegisterHotKey(_hwnd, NativeMethods.HotkeyPlayPause, NativeMethods.ModControl | NativeMethods.ModAlt, (uint)KeyInterop.VirtualKeyFromKey(Key.Space));
        NativeMethods.RegisterHotKey(_hwnd, NativeMethods.HotkeyNextTrack, NativeMethods.ModControl | NativeMethods.ModAlt, (uint)KeyInterop.VirtualKeyFromKey(Key.Right));
        _hotkeysRegistered = true;
    }

    private void RegisterBackgroundHotkey()
    {
        if (_hotkeysRegistered || _hwnd == IntPtr.Zero || _settings.Current.LikeHotkeyVirtualKey == 0)
        {
            return;
        }

        NativeMethods.RegisterHotKey(_hwnd, NativeMethods.HotkeyCustomLike, 0, _settings.Current.LikeHotkeyVirtualKey);
        _hotkeysRegistered = true;
    }

    private void UnregisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HotkeyToggleLike);
        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HotkeyToggleVisibility);
        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HotkeyPreviousTrack);
        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HotkeyPlayPause);
        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HotkeyNextTrack);
        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HotkeyCustomLike);
        _hotkeysRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == NativeMethods.ShowExistingWindowMessage)
        {
            handled = true;
            BringMainWindowToFront();
            return IntPtr.Zero;
        }

        if (msg != NativeMethods.WmHotkey)
        {
            return IntPtr.Zero;
        }

        handled = true;
        switch (wParam.ToInt32())
        {
            case NativeMethods.HotkeyToggleLike:
            case NativeMethods.HotkeyCustomLike:
                _ = ToggleLikeAsync();
                break;
            case NativeMethods.HotkeyToggleVisibility:
                ToggleVisibility();
                break;
            case NativeMethods.HotkeyPreviousTrack:
                _ = RunTrackSwitchCommandAsync(() => _spotify.PreviousTrackAsync(), "Предыдущий трек");
                break;
            case NativeMethods.HotkeyPlayPause:
                if (_current is not null)
                {
                    _ = RunPlaybackCommandAsync(() => _spotify.TogglePlaybackAsync(_current.IsPlaying));
                }
                break;
            case NativeMethods.HotkeyNextTrack:
                _ = RunTrackSwitchCommandAsync(() => _spotify.NextTrackAsync(), "Следующий трек");
                break;
        }

        return IntPtr.Zero;
    }

    private void BringMainWindowToFront()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        NativeMethods.BringWindowToFront(_hwnd);
        KeepAboveWindows();
    }

    private void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        BringMainWindowToFront();
    }

    private void ShowToastIfEnabled(ToastKind kind, PlaybackSnapshot snapshot, string action, string? extra = null)
    {
        if (_settings.Current.OverlayEnabled)
        {
            return;
        }

        var enabled = kind switch
        {
            ToastKind.Like => _settings.Current.NotifyOnLikeChange,
            ToastKind.ManualTrack => _settings.Current.NotifyOnManualTrackChange,
            ToastKind.AutomaticTrack => _settings.Current.NotifyOnAutomaticTrackChange,
            _ => false
        };

        if (!enabled)
        {
            return;
        }

        _toastWindow?.Close();
        _toastWindow = new ToastWindow(snapshot, action, extra);
        _toastWindow.Closed += (_, _) => _toastWindow = null;
        _toastWindow.Show();
    }

    private void KeepAboveWindows()
    {
        if (!IsVisible || !_settings.Current.OverlayEnabled || _settings.Current.SafeMode)
        {
            return;
        }

        Topmost = true;
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.ForceTopmost(_hwnd);
        }
    }

    private void RestoreWindowPosition()
    {
        var left = _settings.Current.WindowLeft;
        var top = _settings.Current.WindowTop;
        if (left.HasValue && top.HasValue && IsOnVirtualScreen(left.Value, top.Value))
        {
            Left = left.Value;
            Top = top.Value;
            return;
        }

        var workArea = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        Left = workArea.Left + (workArea.Width - Width) / 2.0;
        Top = workArea.Top + (workArea.Height - Height) / 2.0;
    }

    private void SaveWindowPosition()
    {
        _settings.Current.WindowLeft = Left;
        _settings.Current.WindowTop = Top;
        _settings.Save();
    }

    private static bool IsOnVirtualScreen(double left, double top)
    {
        return left >= SystemParameters.VirtualScreenLeft - 20
            && top >= SystemParameters.VirtualScreenTop - 20
            && left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 60
            && top <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60;
    }

    private void SetPlaybackButtonsEnabled(bool isEnabled)
    {
        PreviousButton.IsEnabled = isEnabled;
        PlayPauseButton.IsEnabled = isEnabled;
        NextButton.IsEnabled = isEnabled;
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static string Shorten(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 1)] + "...";
    }

    private enum ToastKind
    {
        Like,
        ManualTrack,
        AutomaticTrack
    }
}
