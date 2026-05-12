using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace SpotifyFavoritesTool;

public partial class MainWindow : Window
{
    private static readonly TimeSpan TrackMonitorInterval = TimeSpan.FromSeconds(8);

    private readonly SettingsStore _settings = new();
    private readonly SpotifyAuthService _auth;
    private readonly SpotifyClient _spotify;
    private readonly FavoriteTrackService _favorites;
    private readonly ActivityLog _activityLog = new();
    private readonly HotkeyManager _hotkeys = new();
    private readonly MediaKeyInterceptor _mediaKeyInterceptor = new();
    private readonly ToastPresenter _toasts = new();
    private readonly AsyncActionGate _userActionGate = new();
    private readonly AsyncActionGate _trackMonitorGate = new();

    private SettingsWindow? _settingsWindow;
    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _isExiting;
    private DispatcherTimer? _trackMonitorTimer;
    private DispatcherTimer? _mediaKeyRegistrationRetryTimer;
    private TrayIconController? _trayIcon;
    private string _lastMediaKeyRegistrationSummary = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        _auth = new SpotifyAuthService(_settings);
        _spotify = new SpotifyClient(_auth);
        _favorites = new FavoriteTrackService(_spotify);
        ActivityLogList.ItemsSource = _activityLog.Entries;
        _mediaKeyInterceptor.MediaKeyPressed += MediaKeyInterceptor_MediaKeyPressed;
        _trayIcon = new TrayIconController(Dispatcher, BringMainWindowToFront, ShowSettingsWindow, ExitApplication);
        Log("Приложение запущено.");
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowPosition();
        StartTrackMonitorIfReady();
        UpdateStatus();
        Dispatcher.BeginInvoke(RegisterHotkeys, DispatcherPriority.ApplicationIdle);
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
            Log("Окно скрыто в трей.");
            return;
        }

        StopTrackMonitor(clearCache: false);
        StopMediaKeyRegistrationRetry();
        SaveWindowPosition();
        _mediaKeyInterceptor.Dispose();
        _hotkeys.Unregister();
        _source?.RemoveHook(WndProc);
        _trayIcon?.Dispose();
        _toasts.Dispose();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsWindow();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        Log("Окно свернуто.");
    }

    private void CloseToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            e.Handled = true;
        }
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

    private void ShowSettingsWindow()
    {
        BringMainWindowToFront();

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            Log("Окно настроек уже открыто.");
            return;
        }

        Log("Открыты настройки.");
        _settingsWindow = new SettingsWindow(_settings, _auth)
        {
            Owner = this
        };
        _settingsWindow.AuthChanged += (_, _) =>
        {
            _favorites.ClearCache();
            StartTrackMonitorIfReady();
            UpdateStatus();
            Log(_auth.HasRefreshToken ? "Spotify подключен, кеш треков очищен." : "Spotify отключен, кеш треков очищен.");
        };
        _settingsWindow.SettingsChanged += (_, _) =>
        {
            RegisterHotkeys();
            StartTrackMonitorIfReady();
            UpdateStatus("Настройки сохранены.");
            Log("Настройки сохранены.");
        };
        _settingsWindow.RestartRequested += (_, _) => RestartApplication();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private async Task ToggleFavoriteAsync()
    {
        if (!_userActionGate.TryEnter(out var action))
        {
            return;
        }

        using (action)
        {
            try
            {
                StatusText.Text = "Проверяю текущий трек...";
                Log("Нажата клавиша Избранного: получаю текущий трек.");
                var result = await _favorites.ToggleCurrentTrackAsync();
                StatusText.Text = $"{result.Message}: {result.Track.Name}";
                _toasts.Show(result);
                Log($"Статус Избранного перед изменением: {DescribeFavoriteStatusSource(result.PreviousStatusSource)}.");
                Log($"{result.Message}: {result.Track.Name}.");
            }
            catch (SpotifyRateLimitException ex)
            {
                StopTrackMonitor(clearCache: false);
                StatusText.Text = Shorten(ex.Message, 160);
                _toasts.ShowError("Spotify ограничил запросы", ex.Message);
                Log($"Spotify вернул 429: {ex.Endpoint}.");
            }
            catch (Exception ex)
            {
                StatusText.Text = Shorten(ex.Message, 140);
                _toasts.ShowError("Избранное недоступно", ex.Message);
                Log($"Ошибка Избранного: {Shorten(ex.Message, 90)}");
            }
        }
    }

    private async Task ShowFavoriteStatusAsync()
    {
        if (!_userActionGate.TryEnter(out var action))
        {
            return;
        }

        using (action)
        {
            try
            {
                StatusText.Text = "Проверяю текущий трек...";
                Log("Нажата клавиша статуса: получаю текущий трек.");
                var result = await _favorites.GetCurrentTrackWithFavoriteStatusAsync();
                if (result is null)
                {
                    StatusText.Text = "Spotify сейчас ничего не играет.";
                    _toasts.ShowError("Статус Избранного неизвестен", "Spotify сейчас ничего не играет.");
                    Log("Статус не показан: Spotify сейчас ничего не играет.");
                    return;
                }

                ShowFavoriteStatusToast(result.Track);
                Log($"Статус Избранного получен: {DescribeFavoriteStatusSource(result.Source)}.");
                Log($"Показан статус Избранного: {result.Track.Name}.");
            }
            catch (SpotifyRateLimitException ex)
            {
                StopTrackMonitor(clearCache: false);
                StatusText.Text = Shorten(ex.Message, 160);
                _toasts.ShowError("Spotify ограничил запросы", ex.Message);
                Log($"Spotify вернул 429: {ex.Endpoint}.");
            }
            catch (Exception ex)
            {
                StatusText.Text = Shorten(ex.Message, 140);
                _toasts.ShowError("Статус Избранного недоступен", ex.Message);
                Log($"Ошибка статуса: {Shorten(ex.Message, 90)}");
            }
        }
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
            Log("Мониторинг трека запущен.");
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
        var wasEnabled = _trackMonitorTimer?.IsEnabled == true;
        _trackMonitorTimer?.Stop();
        if (clearCache)
        {
            _favorites.ClearCache();
        }

        if (wasEnabled)
        {
            Log(clearCache ? "Мониторинг трека остановлен, кеш очищен." : "Мониторинг трека остановлен.");
        }
    }

    private async void TrackMonitorTimer_Tick(object? sender, EventArgs e)
    {
        await CheckTrackChangeAsync();
    }

    private async Task CheckTrackChangeAsync()
    {
        if (!_auth.HasRefreshToken || !_trackMonitorGate.TryEnter(out var action))
        {
            return;
        }

        using (action)
        {
            try
            {
                var result = await _favorites.GetChangedTrackWithFavoriteStatusAsync();
                if (result is not null)
                {
                    ShowTrackChangedToast(result.Track);
                    Log($"Трек сменился: {result.Track.Name}.");
                    Log($"Статус Избранного для нового трека: {DescribeFavoriteStatusSource(result.Source)}.");
                }
            }
            catch (SpotifyRateLimitException ex)
            {
                StopTrackMonitor(clearCache: false);
                StatusText.Text = Shorten(ex.Message, 160);
                _toasts.ShowError("Spotify ограничил запросы", ex.Message);
                Log($"Мониторинг остановлен: Spotify вернул 429 ({ex.Endpoint}).");
            }
            catch (Exception ex)
            {
                StopTrackMonitor(clearCache: false);
                StatusText.Text = Shorten($"Мониторинг трека остановлен: {ex.Message}", 160);
                Log($"Мониторинг остановлен: {Shorten(ex.Message, 90)}");
            }
        }
    }

    private void ShowFavoriteStatusToast(PlaybackTrack track)
    {
        var isLiked = track.IsLiked == true;
        var message = isLiked ? "Уже в Избранном" : "Не в Избранном";
        StatusText.Text = $"{message}: {track.Name}";
        _toasts.ShowFavoriteStatus(track);
    }

    private void ShowTrackChangedToast(PlaybackTrack track)
    {
        _toasts.ShowTrackChanged(track);
    }

    private void RegisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        _hotkeys.Register(_hwnd, _settings.Current);
        ApplyMediaKeyInterceptor();
        var registration = _hotkeys.GetRegistrationMessage();
        Log(string.IsNullOrWhiteSpace(registration)
            ? "Горячие клавиши зарегистрированы."
            : $"Горячие клавиши зарегистрированы с предупреждением:{registration}");
    }

    private void ApplyMediaKeyInterceptor()
    {
        var wasEnabled = _mediaKeyInterceptor.IsEnabled;
        var isApplied = _mediaKeyInterceptor.Apply(
            _settings.Current.KeepMediaKeysLocalDuringRdp,
            _hwnd,
            GetApplicationHotkeys());

        if (!_settings.Current.KeepMediaKeysLocalDuringRdp)
        {
            StopMediaKeyRegistrationRetry();
            if (wasEnabled)
            {
                Log("RDP-перехват медиа-клавиш отключен.");
            }

            return;
        }

        if (!isApplied)
        {
            StartMediaKeyRegistrationRetry();
            Log($"RDP-перехват медиа-клавиш не включился: Win32 {_mediaKeyInterceptor.LastInstallError}.");
            return;
        }

        LogMediaKeyRegistrationSummaryIfChanged();
        if (_mediaKeyInterceptor.ShouldRetryRegistration)
        {
            StartMediaKeyRegistrationRetry();
        }
        else
        {
            StopMediaKeyRegistrationRetry();
        }

        if (!wasEnabled)
        {
            Log("RDP-перехват медиа-клавиш включен.");
        }
    }

    private void StartMediaKeyRegistrationRetry()
    {
        _mediaKeyRegistrationRetryTimer ??= CreateMediaKeyRegistrationRetryTimer();
        if (!_mediaKeyRegistrationRetryTimer.IsEnabled)
        {
            _mediaKeyRegistrationRetryTimer.Start();
        }
    }

    private DispatcherTimer CreateMediaKeyRegistrationRetryTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        timer.Tick += (_, _) => RetryMediaKeyRegistration();
        return timer;
    }

    private void RetryMediaKeyRegistration()
    {
        if (!_settings.Current.KeepMediaKeysLocalDuringRdp || _hwnd == IntPtr.Zero)
        {
            StopMediaKeyRegistrationRetry();
            return;
        }

        _mediaKeyInterceptor.Apply(true, _hwnd, GetApplicationHotkeys());
        LogMediaKeyRegistrationSummaryIfChanged();
        if (!_mediaKeyInterceptor.ShouldRetryRegistration)
        {
            StopMediaKeyRegistrationRetry();
        }
    }

    private void StopMediaKeyRegistrationRetry()
    {
        _mediaKeyRegistrationRetryTimer?.Stop();
    }

    private void LogMediaKeyRegistrationSummaryIfChanged()
    {
        var summary = _mediaKeyInterceptor.RegistrationSummary;
        if (summary == _lastMediaKeyRegistrationSummary)
        {
            return;
        }

        _lastMediaKeyRegistrationSummary = summary;
        Log(summary);
    }

    private IEnumerable<uint> GetApplicationHotkeys()
    {
        if (_settings.Current.LikeHotkeyVirtualKey != 0)
        {
            yield return _settings.Current.LikeHotkeyVirtualKey;
        }

        if (_settings.Current.FavoriteStatusHotkeyVirtualKey != 0)
        {
            yield return _settings.Current.FavoriteStatusHotkeyVirtualKey;
        }
    }

    private void MediaKeyInterceptor_MediaKeyPressed(object? sender, MediaKeyPressedEventArgs e)
    {
        if (e.VirtualKey == _settings.Current.LikeHotkeyVirtualKey)
        {
            e.Handled = true;
            Dispatcher.BeginInvoke(() => _ = ToggleFavoriteAsync());
            return;
        }

        if (e.VirtualKey == _settings.Current.FavoriteStatusHotkeyVirtualKey)
        {
            e.Handled = true;
            Dispatcher.BeginInvoke(() => _ = ShowFavoriteStatusAsync());
        }
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

        if (_mediaKeyInterceptor.TryHandleHotkey(wParam))
        {
            handled = true;
            return IntPtr.Zero;
        }

        if (!_hotkeys.TryGetAction(wParam, out var action))
        {
            return IntPtr.Zero;
        }

        handled = true;
        switch (action)
        {
            case HotkeyAction.ToggleFavorite:
                _ = ToggleFavoriteAsync();
                break;
            case HotkeyAction.ShowFavoriteStatus:
                _ = ShowFavoriteStatusAsync();
                break;
        }

        return IntPtr.Zero;
    }

    private void BringMainWindowToFront()
    {
        if (!IsVisible)
        {
            Show();
            Log("Окно восстановлено из трея.");
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

        var registration = _hotkeys.GetRegistrationMessage();
        var monitor = _trackMonitorTimer?.IsEnabled == true ? " Мониторинг трека: каждые 8 секунд." : string.Empty;
        var hint = $"Клавиша статуса получает текущий трек через Spotify API; Избранное проверяется только если трека еще нет в кеше.{monitor}{registration}";
        StatusText.Text = string.IsNullOrWhiteSpace(prefix)
            ? $"{account} {hint}"
            : $"{prefix} {account} {hint}";
    }

    private void ExitApplication()
    {
        Log("Выход из приложения.");
        _isExiting = true;
        Close();
    }

    private void RestartApplication()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        var escapedPath = executablePath.Replace("'", "''", StringComparison.Ordinal);
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -WindowStyle Hidden -Command \"Start-Sleep -Milliseconds 900; Start-Process -FilePath '{escapedPath}'\"",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false
        });

        Log("Перезапуск приложения для применения перехвата медиа-клавиш.");
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

    private static string DescribeFavoriteStatusSource(FavoriteStatusSource source)
    {
        return source switch
        {
            FavoriteStatusSource.Cache => "из кеша",
            FavoriteStatusSource.SpotifyApi => "через Spotify API",
            _ => "неизвестно"
        };
    }

    private void Log(string message)
    {
        _activityLog.Add(message);
    }
}
