using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace SpotifyRelayOverlay;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settings = new();
    private readonly SpotifyAuthService _auth;
    private readonly SpotifyClient _spotify;

    private SettingsWindow? _settingsWindow;
    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _hotkeyRegistered;
    private bool _hotkeyRegistrationFailed;
    private bool _isExecuting;
    private bool _isExiting;
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
        UpdateStatus();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        RegisterFavoriteHotkey();
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
        UnregisterFavoriteHotkey();
        _source?.RemoveHook(WndProc);
        _trayIcon?.Dispose();
        _toastWindow?.Close();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsWindow();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ExitApplication();
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
        _settingsWindow.AuthChanged += (_, _) => UpdateStatus();
        _settingsWindow.SettingsChanged += (_, _) =>
        {
            RegisterFavoriteHotkey();
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
            StatusText.Text = $"{result.Message}: {result.Track.Name}";
            ShowToast(result);
        }
        catch (SpotifyRateLimitException ex)
        {
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

    private void ShowToast(FavoriteToggleResult result)
    {
        _toastWindow?.Close();
        _toastWindow = new ToastWindow(result);
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

    private void RegisterFavoriteHotkey()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        UnregisterFavoriteHotkey();
        if (_settings.Current.LikeHotkeyVirtualKey == 0)
        {
            return;
        }

        _hotkeyRegistered = NativeMethods.RegisterHotKey(
            _hwnd,
            NativeMethods.HotkeyToggleFavorite,
            0,
            _settings.Current.LikeHotkeyVirtualKey);
        _hotkeyRegistrationFailed = !_hotkeyRegistered;
    }

    private void UnregisterFavoriteHotkey()
    {
        if (_hwnd == IntPtr.Zero || !_hotkeyRegistered)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HotkeyToggleFavorite);
        _hotkeyRegistered = false;
        _hotkeyRegistrationFailed = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == NativeMethods.ShowExistingWindowMessage)
        {
            handled = true;
            BringMainWindowToFront();
            return IntPtr.Zero;
        }

        if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == NativeMethods.HotkeyToggleFavorite)
        {
            handled = true;
            _ = ToggleFavoriteAsync();
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
        var hotkey = _settings.Current.LikeHotkeyVirtualKey == 0
            ? "не назначена"
            : $"0x{_settings.Current.LikeHotkeyVirtualKey:X2}";
        HotkeyText.Text = $"Клавиша Избранного: {hotkey}";

        var account = _auth.HasRefreshToken ? "Spotify подключен." : "Spotify не подключен.";
        var registration = _hotkeyRegistrationFailed ? " Клавиша занята или недоступна." : string.Empty;
        var hint = $"По нажатию клавиши приложение проверит текущий трек, изменит Избранное и покажет уведомление.{registration}";
        StatusText.Text = string.IsNullOrWhiteSpace(prefix)
            ? $"{account} {hint}"
            : $"{prefix} {account} {hint}";
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
