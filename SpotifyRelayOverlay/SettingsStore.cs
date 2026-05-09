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
        Current = Load();
    }

    public string SettingsDirectory { get; }
    public string SettingsPath { get; }
    public AppSettings Current { get; private set; }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public void ClearTokens()
    {
        Current.AccessToken = string.Empty;
        Current.RefreshToken = string.Empty;
        Current.GrantedScopes = string.Empty;
        Current.AccessTokenExpiresAt = DateTimeOffset.MinValue;
        Save();
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
