using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace SpotifyRelayOverlay;

public partial class MainWindow : Window
{
    private static readonly TimeSpan TrackMonitorInterval = TimeSpan.FromSeconds(8);

    private readonly SettingsStore _settings = new();
    private readonly SpotifyAuthService _auth;
    private readonly SpotifyClient _spotify;

    private SettingsWindow? _settingsWindow;
    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _favoriteHotkeyRegistered;
    private bool _favoriteHotkeyRegistrationFailed;
    private bool _statusHotkeyRegistered;
    private bool _statusHotkeyRegistrationFailed;
    private bool _isExecuting;
    private bool _isCheckingTrack;
    private bool _isExiting;
    private PlaybackTrack? _cachedTrack;
    private bool _cachedTrackIsLiked;
    private string? _lastObservedTrackUri;
    private DispatcherTimer? _trackMonitorTimer;
    private Forms.NotifyIcon? _trayIcon;
    private ToastWindow? _toastWindow;

    public MainWindow()
    {
        InitializeComponent();
        _auth = new SpotifyAuthService(_settings);
        _spotify = new SpotifyClient(_auth);
        InitializeTrayIcon();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowPosition();
        StartTrackMonitorIfReady();
        UpdateStatus();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        RegisterHotkeys();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        StopTrackMonitor(clearCache: false);
        SaveWindowPosition();
        UnregisterHotkeys();
        _source?.RemoveHook(WndProc);
        _trayIcon?.Dispose();
        _toastWindow?.Close();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsWindow();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if Windows has already ended the mouse capture.
        }
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
            Text = "Spotify Избранное",
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
            Owner = this
        };
        _settingsWindow.AuthChanged += (_, _) =>
        {
            _lastObservedTrackUri = null;
            StartTrackMonitorIfReady();
            UpdateStatus();
        };
        _settingsWindow.SettingsChanged += (_, _) =>
        {
            RegisterHotkeys();
            StartTrackMonitorIfReady();
            UpdateStatus("Настройки сохранены.");
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private async Task ToggleFavoriteAsync()
    {
        if (_isExecuting)
        {
            return;
        }

        _isExecuting = true;
        try
        {
            StatusText.Text = "Проверяю текущий трек...";
            var result = await _spotify.ToggleCurrentTrackFavoriteAsync();
            CacheTrackState(result.Track, result.IsLiked);
            StatusText.Text = $"{result.Message}: {result.Track.Name}";
            ShowToast(result);
        }
        catch (SpotifyRateLimitException ex)
        {
            StopTrackMonitor(clearCache: false);
            StatusText.Text = Shorten(ex.Message, 160);
            ShowErrorToast("Spotify ограничил запросы", ex.Message);
        }
        catch (Exception ex)
        {
            StatusText.Text = Shorten(ex.Message, 140);
            ShowErrorToast("Избранное недоступно", ex.Message);
        }
        finally
        {
            _isExecuting = false;
        }
    }

    private void ShowCachedFavoriteStatus()
    {
        if (_cachedTrack is null)
        {
            ShowErrorToast(
                "Статус Избранного неизвестен",
                "Кеш еще пуст. Дождись уведомления о смене трека или один раз измени Избранное горячей клавишей.");
            return;
        }

        var message = _cachedTrackIsLiked ? "Уже в Избранном" : "Не в Избранном";
        _toastWindow?.Close();
        _toastWindow = new ToastWindow(_cachedTrack, _cachedTrackIsLiked, message);
        _toastWindow.Closed += (_, _) => _toastWindow = null;
        _toastWindow.Show();
    }

    private void StartTrackMonitorIfReady()
    {
        if (!_auth.HasRefreshToken || _isExiting)
        {
            StopTrackMonitor(clearCache: true);
            return;
        }

        _trackMonitorTimer ??= CreateTrackMonitorTimer();
        if (!_trackMonitorTimer.IsEnabled)
        {
            _trackMonitorTimer.Start();
        }
    }

    private DispatcherTimer CreateTrackMonitorTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TrackMonitorInterval
        };
        timer.Tick += TrackMonitorTimer_Tick;
        return timer;
    }

    private void StopTrackMonitor(bool clearCache)
    {
        _trackMonitorTimer?.Stop();
        if (clearCache)
        {
            _lastObservedTrackUri = null;
            _cachedTrack = null;
            _cachedTrackIsLiked = false;
        }
    }

    private async void TrackMonitorTimer_Tick(object? sender, EventArgs e)
    {
        await CheckTrackChangeAsync();
    }

    private async Task CheckTrackChangeAsync()
    {
        if (_isCheckingTrack || !_auth.HasRefreshToken)
        {
            return;
        }

        _isCheckingTrack = true;
        try
        {
            var track = await _spotify.GetCurrentTrackOrNullAsync();
            if (track is null)
            {
                _lastObservedTrackUri = null;
                return;
            }

            if (string.Equals(_lastObservedTrackUri, track.Uri, StringComparison.Ordinal))
            {
                return;
            }

            var isLiked = await _spotify.GetTrackLikedStateAsync(track);
            CacheTrackState(track, isLiked);
            ShowTrackChangedToast(track, isLiked);
        }
        catch (SpotifyRateLimitException ex)
        {
            StopTrackMonitor(clearCache: false);
            StatusText.Text = Shorten(ex.Message, 160);
            ShowErrorToast("Spotify ограничил запросы", ex.Message);
        }
        catch (Exception ex)
        {
            StopTrackMonitor(clearCache: false);
            StatusText.Text = Shorten($"Мониторинг трека остановлен: {ex.Message}", 160);
        }
        finally
        {
            _isCheckingTrack = false;
        }
    }

    private void CacheTrackState(PlaybackTrack track, bool isLiked)
    {
        _cachedTrack = track;
        _cachedTrackIsLiked = isLiked;
        _lastObservedTrackUri = track.Uri;
    }

    private void ShowToast(FavoriteToggleResult result)
    {
        _toastWindow?.Close();
        _toastWindow = new ToastWindow(result);
        _toastWindow.Closed += (_, _) => _toastWindow = null;
        _toastWindow.Show();
    }

    private void ShowTrackChangedToast(PlaybackTrack track, bool isLiked)
    {
        _toastWindow?.Close();
        _toastWindow = new ToastWindow(track, isLiked);
        _toastWindow.Closed += (_, _) => _toastWindow = null;
        _toastWindow.Show();
    }

    private void ShowErrorToast(string title, string message)
    {
        _toastWindow?.Close();
        _toastWindow = new ToastWindow(title, message);
        _toastWindow.Closed += (_, _) => _toastWindow = null;
        _toastWindow.Show();
    }

    private void RegisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        UnregisterHotkeys();
        RegisterFavoriteHotkey();
        RegisterStatusHotkey();
    }

    private void RegisterFavoriteHotkey()
    {
        if (_settings.Current.LikeHotkeyVirtualKey == 0)
        {
            return;
        }

        _favoriteHotkeyRegistered = NativeMethods.RegisterHotKey(
            _hwnd,
            NativeMethods.HotkeyToggleFavorite,
            0,
            _settings.Current.LikeHotkeyVirtualKey);
        _favoriteHotkeyRegistrationFailed = !_favoriteHotkeyRegistered;
    }

    private void RegisterStatusHotkey()
    {
        if (_settings.Current.FavoriteStatusHotkeyVirtualKey == 0)
        {
            return;
        }

        _statusHotkeyRegistered = NativeMethods.RegisterHotKey(
            _hwnd,
            NativeMethods.HotkeyShowFavoriteStatus,
            0,
            _settings.Current.FavoriteStatusHotkeyVirtualKey);
        _statusHotkeyRegistrationFailed = !_statusHotkeyRegistered;
    }

    private void UnregisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        if (_favoriteHotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HotkeyToggleFavorite);
            _favoriteHotkeyRegistered = false;
        }

        if (_statusHotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HotkeyShowFavoriteStatus);
            _statusHotkeyRegistered = false;
        }

        _favoriteHotkeyRegistrationFailed = false;
        _statusHotkeyRegistrationFailed = false;
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

        if (wParam.ToInt32() == NativeMethods.HotkeyToggleFavorite)
        {
            handled = true;
            _ = ToggleFavoriteAsync();
            return IntPtr.Zero;
        }

        if (wParam.ToInt32() == NativeMethods.HotkeyShowFavoriteStatus)
        {
            handled = true;
            ShowCachedFavoriteStatus();
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
    }

    private void UpdateStatus(string? prefix = null)
    {
        var favoriteHotkey = !string.IsNullOrWhiteSpace(_settings.Current.LikeHotkeyDisplayName)
            ? _settings.Current.LikeHotkeyDisplayName
            : HotkeyFormatter.Format(_settings.Current.LikeHotkeyVirtualKey);
        var statusHotkey = !string.IsNullOrWhiteSpace(_settings.Current.FavoriteStatusHotkeyDisplayName)
            ? _settings.Current.FavoriteStatusHotkeyDisplayName
            : HotkeyFormatter.Format(_settings.Current.FavoriteStatusHotkeyVirtualKey);
        HotkeyText.Text = $"Избранное: {favoriteHotkey} · Статус: {statusHotkey}";

        var account = _auth.HasRefreshToken ? "Spotify подключен." : "Spotify не подключен.";
        if (_auth.KnowsGrantedScopes && !_auth.HasRequiredScopes)
        {
            account += " Не хватает прав на Избранное: открой настройки, нажми «Выйти», затем «Войти в Spotify».";
        }

        var registration = GetHotkeyRegistrationMessage();
        var monitor = _trackMonitorTimer?.IsEnabled == true ? " Мониторинг трека: каждые 8 секунд." : string.Empty;
        var hint = $"Клавиша статуса показывает только кеш и не делает запросов к Spotify.{monitor}{registration}";
        StatusText.Text = string.IsNullOrWhiteSpace(prefix)
            ? $"{account} {hint}"
            : $"{prefix} {account} {hint}";
    }

    private string GetHotkeyRegistrationMessage()
    {
        var parts = new List<string>();
        if (_favoriteHotkeyRegistrationFailed)
        {
            parts.Add("клавиша Избранного занята или недоступна");
        }

        if (_statusHotkeyRegistrationFailed)
        {
            parts.Add("клавиша статуса занята или недоступна");
        }

        return parts.Count == 0 ? string.Empty : $" {string.Join("; ", parts)}.";
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
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

    private static string Shorten(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 1)] + "...";
    }
}
