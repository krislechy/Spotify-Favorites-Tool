using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SpotifyFavoritesTool;

public sealed partial class LrclibLyricsService
{
    private const string GetEndpoint = "https://lrclib.net/api/get";
    private const string SearchEndpoint = "https://lrclib.net/api/search";
    private const string LyricsOvhEndpoint = "https://api.lyrics.ovh/v1";
    private const string GeniusSearchEndpoint = "https://genius.com/search";
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
        var exactLyrics = await TryLoadExactLyricsAsync(track, cancellationToken);
        if (exactLyrics.Kind != LyricsKind.None)
        {
            return exactLyrics;
        }

        var searchLyrics = await SearchLyricsAsync(track, cancellationToken);
        if (searchLyrics.Kind != LyricsKind.None)
        {
            return searchLyrics;
        }

        var lyricsOvhLyrics = await SearchLyricsOvhAsync(track, cancellationToken);
        if (lyricsOvhLyrics.Kind != LyricsKind.None)
        {
            return lyricsOvhLyrics;
        }

        return BuildGeniusSearchResult(track);
    }

    private static async Task<KaraokeLyrics> TryLoadExactLyricsAsync(PlaybackTrack track, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(BuildExactUrl(track), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return KaraokeLyrics.Empty;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = JsonSerializer.Deserialize<LrclibResponse>(body, JsonOptions);
        return ToLyrics(dto);
    }

    private static async Task<KaraokeLyrics> SearchLyricsAsync(PlaybackTrack track, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(BuildSearchUrl(track), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return KaraokeLyrics.Empty;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var candidates = JsonSerializer.Deserialize<LrclibResponse[]>(body, JsonOptions) ?? [];
        var bestCandidate = candidates
            .Where(HasAnyLyrics)
            .OrderByDescending(candidate => ScoreCandidate(track, candidate))
            .FirstOrDefault();

        return bestCandidate is null ? KaraokeLyrics.Empty : ToLyrics(bestCandidate);
    }

    private static async Task<KaraokeLyrics> SearchLyricsOvhAsync(PlaybackTrack track, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(BuildLyricsOvhUrl(track), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return KaraokeLyrics.Empty;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = JsonSerializer.Deserialize<LyricsOvhResponse>(body, JsonOptions);
        return string.IsNullOrWhiteSpace(dto?.Lyrics)
            ? KaraokeLyrics.Empty
            : new KaraokeLyrics(LyricsKind.Plain, Array.Empty<KaraokeLyricLine>(), dto.Lyrics, "lyrics.ovh");
    }

    private static async Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("SpotifyFavoritesTool/1.0");

        var response = await Http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            var statusCode = (int)response.StatusCode;
            var reason = response.ReasonPhrase;
            response.Dispose();
            throw new InvalidOperationException($"Lyrics service returned {statusCode} {reason}.");
        }

        return response;
    }

    private static KaraokeLyrics ToLyrics(LrclibResponse? dto)
    {
        var syncedLines = LrcLyricsParser.Parse(dto?.SyncedLyrics);
        if (syncedLines.Count > 0)
        {
            return new KaraokeLyrics(LyricsKind.Synced, syncedLines, dto?.PlainLyrics, "LRCLIB");
        }

        return string.IsNullOrWhiteSpace(dto?.PlainLyrics)
            ? KaraokeLyrics.Empty
            : new KaraokeLyrics(LyricsKind.Plain, Array.Empty<KaraokeLyricLine>(), dto.PlainLyrics, "LRCLIB");
    }

    private static string BuildExactUrl(PlaybackTrack track)
    {
        var artist = GetPrimaryArtist(track);
        var parameters = new Dictionary<string, string>
        {
            ["track_name"] = track.Name,
            ["artist_name"] = artist
        };

        if (track.DurationMs is > 0)
        {
            parameters["duration"] = Math.Round(track.DurationMs.Value / 1000d).ToString("0");
        }

        return GetEndpoint + "?" + BuildQuery(parameters);
    }

    private static string BuildSearchUrl(PlaybackTrack track)
    {
        var query = $"{NormalizeSearchText(track.Name)} {NormalizeSearchText(GetPrimaryArtist(track))}".Trim();
        return SearchEndpoint + "?q=" + Uri.EscapeDataString(query);
    }

    private static string BuildLyricsOvhUrl(PlaybackTrack track)
    {
        return $"{LyricsOvhEndpoint}/{Uri.EscapeDataString(GetPrimaryArtist(track))}/{Uri.EscapeDataString(track.Name)}";
    }

    private static KaraokeLyrics BuildGeniusSearchResult(PlaybackTrack track)
    {
        var query = $"{track.Artists} {track.Name}";
        var url = GeniusSearchEndpoint + "?q=" + Uri.EscapeDataString(query);
        var text = "Текст не найден в LRCLIB и lyrics.ovh." + Environment.NewLine +
            "Можно попробовать найти его на Genius:" + Environment.NewLine +
            url;

        return new KaraokeLyrics(LyricsKind.Link, Array.Empty<KaraokeLyricLine>(), text, "Genius", url);
    }

    private static string BuildQuery(Dictionary<string, string> parameters)
    {
        return string.Join("&", parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static string GetPrimaryArtist(PlaybackTrack track)
    {
        return track.Artists.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? track.Artists;
    }

    private static int ScoreCandidate(PlaybackTrack track, LrclibResponse candidate)
    {
        var score = 0;
        if (IsSameText(track.Name, candidate.TrackName))
        {
            score += 60;
        }
        else if (ContainsNormalized(candidate.TrackName, track.Name) || ContainsNormalized(track.Name, candidate.TrackName))
        {
            score += 35;
        }

        if (ContainsNormalized(candidate.ArtistName, track.Artists) || ContainsNormalized(track.Artists, candidate.ArtistName))
        {
            score += 30;
        }

        if (track.DurationMs is > 0 && candidate.Duration is > 0)
        {
            var expectedSeconds = track.DurationMs.Value / 1000d;
            var diff = Math.Abs(expectedSeconds - candidate.Duration.Value);
            score += diff switch
            {
                <= 1 => 20,
                <= 3 => 12,
                <= 7 => 5,
                _ => -20
            };
        }

        if (!string.IsNullOrWhiteSpace(candidate.SyncedLyrics))
        {
            score += 10;
        }

        return score;
    }

    private static bool HasAnyLyrics(LrclibResponse candidate)
    {
        return !string.IsNullOrWhiteSpace(candidate.SyncedLyrics)
            || !string.IsNullOrWhiteSpace(candidate.PlainLyrics);
    }

    private static bool IsSameText(string? left, string? right)
    {
        return string.Equals(NormalizeSearchText(left), NormalizeSearchText(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsNormalized(string? haystack, string? needle)
    {
        var normalizedHaystack = NormalizeSearchText(haystack);
        var normalizedNeedle = NormalizeSearchText(needle);
        return normalizedHaystack.Length > 0
            && normalizedNeedle.Length > 0
            && normalizedHaystack.Contains(normalizedNeedle, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutBrackets = BracketSuffixRegex().Replace(value, " ");
        var withoutNoise = NoiseWordsRegex().Replace(withoutBrackets, " ");
        return WhitespaceRegex().Replace(withoutNoise, " ").Trim();
    }

    private sealed class LrclibResponse
    {
        [JsonPropertyName("trackName")]
        public string? TrackName { get; set; }

        [JsonPropertyName("artistName")]
        public string? ArtistName { get; set; }

        [JsonPropertyName("duration")]
        public double? Duration { get; set; }

        [JsonPropertyName("syncedLyrics")]
        public string? SyncedLyrics { get; set; }

        [JsonPropertyName("plainLyrics")]
        public string? PlainLyrics { get; set; }
    }

    private sealed class LyricsOvhResponse
    {
        [JsonPropertyName("lyrics")]
        public string? Lyrics { get; set; }
    }

    [GeneratedRegex(@"\s*[\(\[].*?(remaster(?:ed)?|deluxe|edition|explicit|clean|radio edit|mono|stereo|version|feat\.?|ft\.?).*?[\)\]]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex BracketSuffixRegex();

    [GeneratedRegex(@"\b(remaster(?:ed)?|deluxe|edition|explicit|clean|radio edit|mono|stereo|version)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NoiseWordsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
