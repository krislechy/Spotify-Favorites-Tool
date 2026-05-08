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
        SafeModeBox.IsChecked = _settings.Current.SafeMode;
        UpdateStatus();
    }

    public event EventHandler? AuthChanged;
    public event EventHandler? SettingsChanged;

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        UpdateStatus("Настройки сохранены.");
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
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
            MessageBox.Show(this, ex.Message, "Spotify Relay Overlay", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    private void SaveSettings()
    {
        _settings.Current.ClientId = ClientIdBox.Text.Trim();
        _settings.Current.SafeMode = SafeModeBox.IsChecked == true;
        _settings.Save();
    }

    private void UpdateStatus(string? prefix = null)
    {
        var authState = _auth.HasRefreshToken ? "Аккаунт подключен." : "Аккаунт не подключен.";
        var safetyState = _settings.Current.SafeMode ? "Безопасный режим включен." : "Безопасный режим выключен.";
        StatusText.Text = string.IsNullOrWhiteSpace(prefix)
            ? $"{authState} {safetyState}"
            : $"{prefix} {authState} {safetyState}";
    }
}
