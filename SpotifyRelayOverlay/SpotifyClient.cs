using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;

namespace SpotifyRelayOverlay;

public sealed class SpotifyClient
{
    private const string ApiRoot = "https://api.spotify.com/v1";
    private static readonly HttpClient Http = new();
    private static readonly TimeSpan MaxRateLimitDelay = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SpotifyAuthService _auth;
    private readonly Dictionary<string, bool> _likedCache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private DateTimeOffset _rateLimitedUntil = DateTimeOffset.MinValue;

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
        var liked = await GetCachedLikeAsync(track.Uri, cancellationToken);
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
        var isLiked = !currentlyLiked;
        _likedCache[track.Uri] = isLiked;
        return isLiked;
    }

    public async Task<bool> IsTrackLikedAsync(PlaybackTrack track, CancellationToken cancellationToken = default)
    {
        var isLiked = await IsLikedAsync(track.Uri, cancellationToken);
        _likedCache[track.Uri] = isLiked;
        return isLiked;
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

    private async Task<bool> GetCachedLikeAsync(string spotifyUri, CancellationToken cancellationToken)
    {
        if (_likedCache.TryGetValue(spotifyUri, out var isLiked))
        {
            return isLiked;
        }

        isLiked = await IsLikedAsync(spotifyUri, cancellationToken);
        _likedCache[spotifyUri] = isLiked;
        return isLiked;
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
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfRateLimited();

            var token = await _auth.GetAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Spotify не подключен. Открой настройки и войди в аккаунт.");
            }

            var refreshedToken = false;
            var rateLimitRetries = 2;
            while (true)
            {
                using var request = new HttpRequestMessage(method, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await Http.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedToken && _auth.HasRefreshToken)
                {
                    token = await _auth.RefreshAccessTokenAsync(cancellationToken);
                    refreshedToken = true;
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        throw new InvalidOperationException("Spotify не подключен. Открой настройки и войди в аккаунт.");
                    }

                    continue;
                }

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    var retryDelay = GetRetryDelay(response);
                    var cooldownDelay = CapRateLimitDelay(retryDelay);
                    _rateLimitedUntil = DateTimeOffset.UtcNow.Add(cooldownDelay);

                    if (rateLimitRetries > 0 && retryDelay <= TimeSpan.FromSeconds(6))
                    {
                        rateLimitRetries--;
                        await Task.Delay(retryDelay + TimeSpan.FromMilliseconds(250), cancellationToken);
                        _rateLimitedUntil = DateTimeOffset.MinValue;
                        continue;
                    }

                    throw CreateRateLimitException(cooldownDelay);
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
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private void ThrowIfRateLimited()
    {
        var remaining = _rateLimitedUntil - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero)
        {
            throw CreateRateLimitException(remaining);
        }
    }

    private static InvalidOperationException CreateRateLimitException(TimeSpan delay)
    {
        return new InvalidOperationException(
            $"Spotify временно ограничил запросы. Подожди {FormatDelay(delay)} и не нажимай команды подряд.");
    }

    private static string FormatDelay(TimeSpan delay)
    {
        if (delay >= TimeSpan.FromMinutes(1))
        {
            return $"{Math.Max(1, (int)Math.Ceiling(delay.TotalMinutes))} мин";
        }

        return $"{Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds))} сек";
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return ClampRetryDelay(delta);
        }

        if (retryAfter?.Date is { } retryDate)
        {
            return ClampRetryDelay(retryDate - DateTimeOffset.UtcNow);
        }

        return TimeSpan.FromSeconds(2);
    }

    private static TimeSpan ClampRetryDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.FromSeconds(1))
        {
            return TimeSpan.FromSeconds(1);
        }

        return delay;
    }

    private static TimeSpan CapRateLimitDelay(TimeSpan delay)
    {
        delay = ClampRetryDelay(delay);
        return delay > MaxRateLimitDelay ? MaxRateLimitDelay : delay;
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
