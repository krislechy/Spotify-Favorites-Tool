using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SpotifyRelayOverlay;

public sealed class SpotifyClient
{
    private const string ApiRoot = "https://api.spotify.com/v1";
    private static readonly HttpClient Http = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SpotifyAuthService _auth;

    public SpotifyClient(SpotifyAuthService auth)
    {
        _auth = auth;
    }

    public async Task<PlaybackSnapshot> GetPlaybackAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            HttpMethod.Get,
            $"{ApiRoot}/me/player/currently-playing?additional_types=track",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new PlaybackSnapshot(null, false, false, 0, 0, "Spotify сейчас ничего не играет");
        }

        var playback = JsonSerializer.Deserialize<PlaybackResponse>(response.Body, JsonOptions);
        var item = playback?.Item;
        if (item?.Id is null || item.Uri is null || item.Name is null)
        {
            return new PlaybackSnapshot(null, false, false, 0, 0, "Spotify сейчас ничего не играет");
        }

        if (!string.Equals(item.Type, "track", StringComparison.OrdinalIgnoreCase))
        {
            return new PlaybackSnapshot(null, playback?.IsPlaying == true, false, 0, 0, "Сейчас играет не трек");
        }

        var artists = item.Artists is { Length: > 0 }
            ? string.Join(", ", item.Artists.Select(artist => artist.Name).Where(name => !string.IsNullOrWhiteSpace(name)))
            : "Unknown artist";
        var image = item.Album?.Images?
            .Where(image => !string.IsNullOrWhiteSpace(image.Url))
            .OrderBy(image => Math.Abs((image.Width ?? 300) - 300))
            .FirstOrDefault()
            ?.Url;

        var track = new PlaybackTrack(item.Id, item.Uri, item.Name, artists, image);
        var liked = await IsLikedAsync(track.Uri, cancellationToken);
        return new PlaybackSnapshot(
            track,
            playback?.IsPlaying == true,
            liked,
            playback?.ProgressMs ?? 0,
            item.DurationMs,
            playback?.IsPlaying == true ? "Сейчас играет" : "Пауза");
    }

    public async Task<bool> ToggleLikeAsync(PlaybackTrack track, bool currentlyLiked, CancellationToken cancellationToken = default)
    {
        var uri = Uri.EscapeDataString(track.Uri);
        var method = currentlyLiked ? HttpMethod.Delete : HttpMethod.Put;
        await SendAsync(method, $"{ApiRoot}/me/library?uris={uri}", cancellationToken);
        return !currentlyLiked;
    }

    public async Task<bool> IsTrackLikedAsync(PlaybackTrack track, CancellationToken cancellationToken = default)
    {
        return await IsLikedAsync(track.Uri, cancellationToken);
    }

    public async Task TogglePlaybackAsync(bool isPlaying, CancellationToken cancellationToken = default)
    {
        var action = isPlaying ? "pause" : "play";
        await SendAsync(HttpMethod.Put, $"{ApiRoot}/me/player/{action}", cancellationToken);
    }

    public async Task NextTrackAsync(CancellationToken cancellationToken = default)
    {
        await SendAsync(HttpMethod.Post, $"{ApiRoot}/me/player/next", cancellationToken);
    }

    public async Task PreviousTrackAsync(CancellationToken cancellationToken = default)
    {
        await SendAsync(HttpMethod.Post, $"{ApiRoot}/me/player/previous", cancellationToken);
    }

    private async Task<bool> IsLikedAsync(string spotifyUri, CancellationToken cancellationToken)
    {
        var uri = Uri.EscapeDataString(spotifyUri);
        var response = await SendAsync(HttpMethod.Get, $"{ApiRoot}/me/library/contains?uris={uri}", cancellationToken);
        var values = JsonSerializer.Deserialize<bool[]>(response.Body, JsonOptions);
        return values is { Length: > 0 } && values[0];
    }

    private async Task<ApiResponse> SendAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = attempt == 0
                ? await _auth.GetAccessTokenAsync(cancellationToken)
                : await _auth.RefreshAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Spotify не подключен. Открой настройки и войди в аккаунт.");
            }

            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0 && _auth.HasRefreshToken)
            {
                continue;
            }

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new InvalidOperationException(
                        "Spotify вернул 401. Открой настройки, нажми «Выйти», потом «Войти в Spotify» и выдай новые права на управление.");
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new InvalidOperationException(
                        "Spotify отказал в управлении. Заново войди в настройках для новых прав; для play/pause/skip обычно нужен Premium и активное устройство Spotify.");
                }

                throw new SpotifyApiException(response.StatusCode, body);
            }

            return new ApiResponse(response.StatusCode, body);
        }

        throw new InvalidOperationException("Spotify вернул 401. Заново войди в Spotify в настройках.");
    }

    private sealed record ApiResponse(HttpStatusCode StatusCode, string Body);
}

public sealed class SpotifyApiException : Exception
{
    public SpotifyApiException(HttpStatusCode statusCode, string body)
        : base($"Spotify API вернул {(int)statusCode}: {body}")
    {
        StatusCode = statusCode;
        Body = body;
    }

    public HttpStatusCode StatusCode { get; }
    public string Body { get; }
}
