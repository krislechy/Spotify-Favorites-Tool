using System.Text.Json.Serialization;

namespace SpotifyFavoritesTool;

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
    [JsonPropertyName("is_playing")]
    public bool IsPlaying { get; set; }

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
