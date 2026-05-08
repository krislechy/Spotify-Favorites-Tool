using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace SpotifyRelayOverlay;

public partial class MainWindow : Window
{
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
    private bool _hotkeysRegistered;
    private bool _isExiting;
    private ToastWindow? _toastWindow;
    private Forms.NotifyIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();

        _auth = new SpotifyAuthService(_settings);
        _spotify = new SpotifyClient(_auth);

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
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
        await RefreshPlaybackAsync();
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
        await ToggleLikeAsync(showToast: true);
    }

    private async void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        await RunPlaybackCommandAsync(() => _spotify.PreviousTrackAsync(), "Предыдущий трек");
    }

    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = _current;
        if (snapshot is null)
        {
            return;
        }

        await RunPlaybackCommandAsync(
            () => _spotify.TogglePlaybackAsync(snapshot.IsPlaying),
            snapshot.IsPlaying ? "Пауза" : "Воспроизведение");
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        await RunPlaybackCommandAsync(() => _spotify.NextTrackAsync(), "Следующий трек");
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
        menu.Items.Add("Открыть", null, (_, _) => Dispatcher.Invoke(BringOverlayToFront));
        menu.Items.Add("Настройки", null, (_, _) => Dispatcher.Invoke(ShowSettingsWindow));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Spotify Relay Overlay",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(BringOverlayToFront);
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    private void ShowSettingsWindow()
    {
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
        _settingsWindow.AuthChanged += async (_, _) => await RefreshPlaybackAsync();
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

    private async Task RefreshPlaybackAsync()
    {
        if (_isPolling)
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

            _current = await _spotify.GetPlaybackAsync();
            RenderPlayback(_current);
        }
        catch (Exception ex)
        {
            RenderEmpty("Ошибка Spotify", Shorten(ex.Message, 88), "Ошибка");
        }
        finally
        {
            _isPolling = false;
        }
    }

    private async Task ToggleLikeAsync(bool showToast)
    {
        var snapshot = _current;
        if (snapshot?.Track is null)
        {
            await RefreshPlaybackAsync();
            snapshot = _current;
            if (snapshot?.Track is null)
            {
                return;
            }
        }

        try
        {
            LikeButton.IsEnabled = false;
            var isLiked = await _spotify.ToggleLikeAsync(snapshot.Track, snapshot.IsLiked);
            _current = snapshot with { IsLiked = isLiked };
            RenderPlayback(_current);

            if (showToast)
            {
                ShowToast(_current, isLiked ? "Лайкнуто" : "Лайк убран", isLiked ? "Добавлено в избранное" : "Убрано из избранного");
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Лайк не изменен";
            LikedText.Text = Shorten(ex.Message, 72);
        }
        finally
        {
            LikeButton.IsEnabled = _current?.Track is not null;
        }
    }

    private async Task RunPlaybackCommandAsync(Func<Task> command, string toastAction)
    {
        if (_current?.Track is null)
        {
            await RefreshPlaybackAsync();
            if (_current?.Track is null)
            {
                return;
            }
        }

        try
        {
            SetPlaybackButtonsEnabled(false);
            await command();
            await Task.Delay(450);
            await RefreshPlaybackAsync();
            if (_current is not null)
            {
                ShowToast(_current, toastAction);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Команда не выполнена";
            LikedText.Text = Shorten(ex.Message, 84);
        }
        finally
        {
            SetPlaybackButtonsEnabled(_current?.Track is not null);
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
        PlayPauseButton.ToolTip = snapshot.IsPlaying ? "Пауза" : "Продолжить";
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
        PlayPauseButton.ToolTip = "Пауза / продолжить";
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

        if (_settings.Current.OverlayEnabled)
        {
            if (!IsVisible)
            {
                Show();
            }

            if (_settings.Current.SafeMode)
            {
                _topmostTimer.Stop();
                Topmost = false;
            }
            else
            {
                Topmost = true;
                RegisterOverlayHotkeys();
                if (!_topmostTimer.IsEnabled)
                {
                    _topmostTimer.Start();
                }

                KeepAboveWindows();
            }

            return;
        }

        _topmostTimer.Stop();
        Topmost = false;
        RegisterBackgroundHotkey();
        Hide();
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
            BringOverlayToFront();
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
                _ = ToggleLikeAsync(showToast: true);
                break;
            case NativeMethods.HotkeyToggleVisibility:
                ToggleVisibility();
                break;
            case NativeMethods.HotkeyPreviousTrack:
                _ = RunPlaybackCommandAsync(() => _spotify.PreviousTrackAsync(), "Предыдущий трек");
                break;
            case NativeMethods.HotkeyPlayPause:
                if (_current is not null)
                {
                    _ = RunPlaybackCommandAsync(() => _spotify.TogglePlaybackAsync(_current.IsPlaying), _current.IsPlaying ? "Пауза" : "Воспроизведение");
                }
                break;
            case NativeMethods.HotkeyNextTrack:
                _ = RunPlaybackCommandAsync(() => _spotify.NextTrackAsync(), "Следующий трек");
                break;
        }

        return IntPtr.Zero;
    }

    private void BringOverlayToFront()
    {
        if (!_settings.Current.OverlayEnabled)
        {
            ShowSettingsWindow();
            return;
        }

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

        Show();
        KeepAboveWindows();
    }

    private void ShowToast(PlaybackSnapshot snapshot, string action, string? extra = null)
    {
        if (!_settings.Current.ToastNotificationsEnabled)
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

        Left = SystemParameters.PrimaryScreenWidth - Width - 40;
        Top = 80;
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
}
