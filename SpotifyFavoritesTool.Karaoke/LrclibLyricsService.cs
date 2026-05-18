using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpotifyFavoritesTool;

public sealed class LrclibLyricsService
{
    private const string Endpoint = "https://lrclib.net/api/get";
    private static readonly HttpClient Http = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, KaraokeLyrics> _cache = new(StringComparer.Ordinal);

    public async Task<KaraokeLyrics> GetLyricsAsync(PlaybackTrack track, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(track.Uri, out var cachedLyrics))
        {
            return cachedLyrics;
        }

        var lyrics = await LoadLyricsAsync(track, cancellationToken);
        _cache[track.Uri] = lyrics;
        return lyrics;
    }

    private static async Task<KaraokeLyrics> LoadLyricsAsync(PlaybackTrack track, CancellationToken cancellationToken)
    {
        var url = BuildUrl(track);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("SpotifyFavoritesTool/1.0");

        using var response = await Http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return KaraokeLyrics.Empty;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Сервис текста вернул {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = JsonSerializer.Deserialize<LrclibResponse>(body, JsonOptions);
        var syncedLines = LrcLyricsParser.Parse(dto?.SyncedLyrics);
        if (syncedLines.Count > 0)
        {
            return new KaraokeLyrics(LyricsKind.Synced, syncedLines, dto?.PlainLyrics);
        }

        return string.IsNullOrWhiteSpace(dto?.PlainLyrics)
            ? KaraokeLyrics.Empty
            : new KaraokeLyrics(LyricsKind.Plain, Array.Empty<KaraokeLyricLine>(), dto.PlainLyrics);
    }

    private static string BuildUrl(PlaybackTrack track)
    {
        var artist = track.Artists.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? track.Artists;
        var parameters = new Dictionary<string, string>
        {
            ["track_name"] = track.Name,
            ["artist_name"] = artist
        };

        if (track.DurationMs is > 0)
        {
            parameters["duration"] = Math.Round(track.DurationMs.Value / 1000d).ToString("0");
        }

        return Endpoint + "?" + string.Join("&", parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private sealed class LrclibResponse
    {
        [JsonPropertyName("syncedLyrics")]
        public string? SyncedLyrics { get; set; }

        [JsonPropertyName("plainLyrics")]
        public string? PlainLyrics { get; set; }
    }
}
