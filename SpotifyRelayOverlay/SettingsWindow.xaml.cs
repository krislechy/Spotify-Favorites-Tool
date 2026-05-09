using System.Windows;
using System.Windows.Input;

namespace SpotifyRelayOverlay;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly SpotifyAuthService _auth;
    private uint _favoriteHotkeyVirtualKey;

    public SettingsWindow(SettingsStore settings, SpotifyAuthService auth)
    {
        InitializeComponent();
        _settings = settings;
        _auth = auth;
        _favoriteHotkeyVirtualKey = _settings.Current.LikeHotkeyVirtualKey;

        ClientIdBox.Text = _settings.Current.ClientId;
        RedirectUriBox.Text = SpotifyAuthService.RedirectUri;
        FavoriteHotkeyBox.Text = FormatVirtualKey(_favoriteHotkeyVirtualKey);
        UpdateStatus();
    }

    public event EventHandler? AuthChanged;
    public event EventHandler? SettingsChanged;

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSettings())
        {
            return;
        }

        UpdateStatus("Настройки сохранены.");
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSettings())
        {
            return;
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            LoginButton.IsEnabled = false;
            UpdateStatus("Открыл браузер для входа в Spotify...");
            await _auth.LoginAsync();
            UpdateStatus("Spotify подключен.");
            AuthChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            UpdateStatus("Spotify не подключен.");
            System.Windows.MessageBox.Show(this, ex.Message, "Spotify Избранное", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        _auth.Logout();
        UpdateStatus("Токены Spotify удалены.");
        AuthChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void FavoriteHotkeyBox_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        FavoriteHotkeyBox.Text = "Нажми нужную клавишу...";
    }

    private void FavoriteHotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key
        };

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            FavoriteHotkeyBox.Text = "Эту клавишу не удалось распознать";
            return;
        }

        _favoriteHotkeyVirtualKey = virtualKey;
        FavoriteHotkeyBox.Text = $"{key} ({FormatVirtualKey(virtualKey)})";
    }

    private void ClearHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        _favoriteHotkeyVirtualKey = 0;
        FavoriteHotkeyBox.Text = "Не назначена";
    }

    private bool SaveSettings()
    {
        _settings.Current.ClientId = ClientIdBox.Text.Trim();
        _settings.Current.LikeHotkeyVirtualKey = _favoriteHotkeyVirtualKey;
        _settings.Save();
        return true;
    }

    private void UpdateStatus(string? prefix = null)
    {
        var account = _auth.HasRefreshToken ? "Аккаунт подключен." : "Аккаунт не подключен.";
        StatusText.Text = string.IsNullOrWhiteSpace(prefix)
            ? account
            : $"{prefix} {account}";
    }

    private static string FormatVirtualKey(uint virtualKey)
    {
        if (virtualKey == 0)
        {
            return "Не назначена";
        }

        return virtualKey switch
        {
            0xB0 => "VK_MEDIA_NEXT_TRACK (0xB0)",
            0xB1 => "VK_MEDIA_PREV_TRACK (0xB1)",
            0xB2 => "VK_MEDIA_STOP (0xB2)",
            0xB3 => "VK_MEDIA_PLAY_PAUSE (0xB3)",
            _ => $"0x{virtualKey:X2}"
        };
    }
}
