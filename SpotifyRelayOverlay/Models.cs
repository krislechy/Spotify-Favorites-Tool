using System.Text.Json.Serialization;

namespace SpotifyRelayOverlay;

public sealed class AppSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresAt { get; set; } = DateTimeOffset.MinValue;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool OverlayEnabled { get; set; } = true;
    public bool SafeMode { get; set; }
    public uint LikeHotkeyVirtualKey { get; set; } = 0xB3;
    public bool NotifyOnLikeChange { get; set; } = true;
    public bool NotifyOnManualTrackChange { get; set; } = true;
    public bool NotifyOnAutomaticTrackChange { get; set; } = true;
}

public sealed record PlaybackSnapshot(
    PlaybackTrack? Track,
    bool IsPlaying,
    bool IsLiked,
    int ProgressMs,
    int DurationMs,
    string Status);

public sealed record PlaybackTrack(
    string Id,
    string Uri,
    string Name,
    string Artists,
    string? AlbumImageUrl);

internal sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

internal sealed class PlaybackResponse
{
    [JsonPropertyName("is_playing")]
    public bool IsPlaying { get; set; }

    [JsonPropertyName("progress_ms")]
    public int? ProgressMs { get; set; }

    [JsonPropertyName("item")]
    public SpotifyItem? Item { get; set; }
}

internal sealed class SpotifyItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }

    [JsonPropertyName("artists")]
    public SpotifyArtist[]? Artists { get; set; }

    [JsonPropertyName("album")]
    public SpotifyAlbum? Album { get; set; }
}

internal sealed class SpotifyArtist
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class SpotifyAlbum
{
    [JsonPropertyName("images")]
    public SpotifyImage[]? Images { get; set; }
}

internal sealed class SpotifyImage
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }
}
