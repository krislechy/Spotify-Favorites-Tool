using System.Globalization;
using System.Windows;

namespace SpotifyRelayOverlay;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly SpotifyAuthService _auth;

    public SettingsWindow(SettingsStore settings, SpotifyAuthService auth)
    {
        InitializeComponent();
        _settings = settings;
        _auth = auth;
        ClientIdBox.Text = _settings.Current.ClientId;
        RedirectUriBox.Text = SpotifyAuthService.RedirectUri;
        LikeHotkeyBox.Text = $"0x{_settings.Current.LikeHotkeyVirtualKey:X2}";
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
            System.Windows.MessageBox.Show(this, ex.Message, "Spotify Favorite Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    private bool SaveSettings()
    {
        if (!TryParseVirtualKey(LikeHotkeyBox.Text, out var likeKey))
        {
            System.Windows.MessageBox.Show(this, "Код клавиши должен быть числом: например 0xB3 или 179.", "Spotify Favorite Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _settings.Current.ClientId = ClientIdBox.Text.Trim();
        _settings.Current.LikeHotkeyVirtualKey = likeKey;
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

    private static bool TryParseVirtualKey(string value, out uint virtualKey)
    {
        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out virtualKey);
        }

        return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out virtualKey);
    }
}
