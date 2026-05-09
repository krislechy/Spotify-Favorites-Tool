using System.IO;
using System.Text.Json;

namespace SpotifyRelayOverlay;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public SettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        SettingsDirectory = Path.Combine(appData, "SpotifyRelayOverlay");
        SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
        Current = Load(out var shouldSave);
        if (shouldSave)
        {
            Save();
        }
    }

    public string SettingsDirectory { get; }
    public string SettingsPath { get; }
    public AppSettings Current { get; private set; }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        Current.ProtectedAccessToken = WindowsDataProtection.Protect(Current.AccessToken);
        Current.ProtectedRefreshToken = WindowsDataProtection.Protect(Current.RefreshToken);
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public void ClearTokens()
    {
        Current.AccessToken = string.Empty;
        Current.RefreshToken = string.Empty;
        Current.ProtectedAccessToken = string.Empty;
        Current.ProtectedRefreshToken = string.Empty;
        Current.GrantedScopes = string.Empty;
        Current.AccessTokenExpiresAt = DateTimeOffset.MinValue;
        Save();
    }

    private AppSettings Load(out bool shouldSave)
    {
        shouldSave = false;
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            settings.AccessToken = WindowsDataProtection.Unprotect(settings.ProtectedAccessToken);
            settings.RefreshToken = WindowsDataProtection.Unprotect(settings.ProtectedRefreshToken);

            if (string.IsNullOrEmpty(settings.AccessToken) && TryGetString(root, "AccessToken", out var legacyAccessToken))
            {
                settings.AccessToken = legacyAccessToken;
                shouldSave = true;
            }

            if (string.IsNullOrEmpty(settings.RefreshToken) && TryGetString(root, "RefreshToken", out var legacyRefreshToken))
            {
                settings.RefreshToken = legacyRefreshToken;
                shouldSave = true;
            }

            if (string.IsNullOrWhiteSpace(settings.LikeHotkeyDisplayName))
            {
                settings.LikeHotkeyDisplayName = HotkeyFormatter.Format(settings.LikeHotkeyVirtualKey);
                shouldSave = true;
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrEmpty(value);
    }
}
