using System.Text.Json.Serialization;

namespace SpotifyRelayOverlay;

public sealed class AppSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string GrantedScopes { get; set; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresAt { get; set; } = DateTimeOffset.MinValue;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public uint LikeHotkeyVirtualKey { get; set; } = 0xB3;
}

public sealed record PlaybackTrack(
    string Id,
    string Uri,
    string Name,
    string Artists,
    string? AlbumImageUrl);

public sealed record FavoriteToggleResult(
    PlaybackTrack Track,
    bool IsLiked,
    string Message);

internal sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

internal sealed class PlaybackResponse
{
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
