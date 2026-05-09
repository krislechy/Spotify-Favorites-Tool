using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SpotifyRelayOverlay;

public sealed class SpotifyClient
{
    private const string ApiRoot = "https://api.spotify.com/v1";
    private const int MaxRateLimitAttempts = 3;
    private static readonly HttpClient Http = new();
    private static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxAutomaticRateLimitDelay = TimeSpan.FromSeconds(8);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SpotifyAuthService _auth;
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private string? _lastTrackUri;
    private bool _lastTrackLiked;

    public SpotifyClient(SpotifyAuthService auth)
    {
        _auth = auth;
    }

    public async Task<FavoriteToggleResult> ToggleCurrentTrackFavoriteAsync(CancellationToken cancellationToken = default)
    {
        var track = await GetCurrentTrackAsync(cancellationToken);
        var isLiked = await GetLikedStateAsync(track, cancellationToken);
        var nextLiked = !isLiked;

        await SetTrackLikedAsync(track, nextLiked, cancellationToken);
        _lastTrackUri = track.Uri;
        _lastTrackLiked = nextLiked;

        var message = nextLiked ? "Добавлено в Избранное" : "Убрано из Избранного";
        return new FavoriteToggleResult(track, nextLiked, message);
    }

    private async Task<PlaybackTrack> GetCurrentTrackAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            HttpMethod.Get,
            $"{ApiRoot}/me/player/currently-playing?additional_types=track",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            throw new InvalidOperationException("Spotify сейчас ничего не играет.");
        }

        var playback = JsonSerializer.Deserialize<PlaybackResponse>(response.Body, JsonOptions);
        var item = playback?.Item;
        if (item?.Id is null || item.Uri is null || item.Name is null)
        {
            throw new InvalidOperationException("Не удалось получить текущий трек Spotify.");
        }

        if (!string.Equals(item.Type, "track", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Сейчас играет не трек.");
        }

        return CreateTrack(item);
    }

    private async Task<bool> GetLikedStateAsync(PlaybackTrack track, CancellationToken cancellationToken)
    {
        if (string.Equals(_lastTrackUri, track.Uri, StringComparison.Ordinal))
        {
            return _lastTrackLiked;
        }

        return await IsTrackLikedAsync(track, cancellationToken);
    }

    private async Task<bool> IsTrackLikedAsync(PlaybackTrack track, CancellationToken cancellationToken)
    {
        var uri = Uri.EscapeDataString(track.Uri);
        var response = await SendAsync(HttpMethod.Get, $"{ApiRoot}/me/library/contains?uris={uri}", cancellationToken);
        var values = JsonSerializer.Deserialize<bool[]>(response.Body, JsonOptions);
        var isLiked = values is { Length: > 0 } && values[0];
        _lastTrackUri = track.Uri;
        _lastTrackLiked = isLiked;
        return isLiked;
    }

    private async Task SetTrackLikedAsync(PlaybackTrack track, bool isLiked, CancellationToken cancellationToken)
    {
        if (_auth.KnowsGrantedScopes && !_auth.HasRequiredScopes)
        {
            throw new InvalidOperationException(BuildScopeError());
        }

        var uri = Uri.EscapeDataString(track.Uri);
        var method = isLiked ? HttpMethod.Put : HttpMethod.Delete;
        await SendAsync(method, $"{ApiRoot}/me/library?uris={uri}", cancellationToken);
    }

    private static PlaybackTrack CreateTrack(SpotifyItem item)
    {
        var artists = item.Artists is { Length: > 0 }
            ? string.Join(", ", item.Artists.Select(artist => artist.Name).Where(name => !string.IsNullOrWhiteSpace(name)))
            : "Unknown artist";
        var image = item.Album?.Images?
            .Where(image => !string.IsNullOrWhiteSpace(image.Url))
            .OrderBy(image => Math.Abs((image.Width ?? 300) - 300))
            .FirstOrDefault()
            ?.Url;

        return new PlaybackTrack(item.Id!, item.Uri!, item.Name!, artists, image);
    }

    private async Task<ApiResponse> SendAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            var token = await _auth.GetAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Spotify не подключен. Открой настройки и войди в аккаунт.");
            }

            for (var attempt = 1; attempt <= MaxRateLimitAttempts; attempt++)
            {
                using var request = new HttpRequestMessage(method, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await Http.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.StatusCode == (HttpStatusCode)429)
                {
                    var delay = GetRateLimitDelay(response, attempt);
                    var canRetry = attempt < MaxRateLimitAttempts && delay <= MaxAutomaticRateLimitDelay;
                    if (canRetry)
                    {
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    throw new SpotifyRateLimitException(delay, DescribeEndpoint(url));
                }

                if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
                {
                    throw CreateApiException(response.StatusCode, body, url);
                }

                return new ApiResponse(response.StatusCode, body);
            }

            throw new SpotifyRateLimitException(MaxAutomaticRateLimitDelay, DescribeEndpoint(url));
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private static Exception CreateApiException(HttpStatusCode statusCode, string body, string url)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => new InvalidOperationException(
                "Spotify вернул 401. Открой настройки, нажми «Выйти», потом «Войти в Spotify» и выдай новые права."),
            HttpStatusCode.Forbidden => new InvalidOperationException(
                $"{BuildScopeError()} Endpoint: {DescribeEndpoint(url)}. Ответ Spotify: {TrimBody(body)}"),
            _ => new SpotifyApiException(statusCode, body)
        };
    }

    private static string BuildScopeError()
    {
        return $"Spotify вернул 403 Forbidden. Для Избранного нужны права {SpotifyAuthService.RequiredScopes}. " +
            "Открой настройки, нажми «Выйти», затем «Войти в Spotify» и подтверди доступ.";
    }

    private static TimeSpan GetRateLimitDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return ClampRateLimitDelay(delta);
        }

        if (retryAfter?.Date is { } date)
        {
            return ClampRateLimitDelay(date - DateTimeOffset.UtcNow);
        }

        return TimeSpan.FromSeconds(DefaultRateLimitDelay.TotalSeconds * Math.Pow(2, attempt - 1));
    }

    private static TimeSpan ClampRateLimitDelay(TimeSpan delay)
    {
        return delay < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : delay;
    }

    private static string DescribeEndpoint(string url)
    {
        if (url.Contains("/me/library/contains", StringComparison.OrdinalIgnoreCase))
        {
            return "проверка Избранного";
        }

        if (url.Contains("/me/library", StringComparison.OrdinalIgnoreCase))
        {
            return "изменение Избранного";
        }

        if (url.Contains("/me/player/currently-playing", StringComparison.OrdinalIgnoreCase))
        {
            return "получение текущего трека";
        }

        return "Spotify API";
    }

    private static string TrimBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "пустой ответ";
        }

        return body.Length <= 220 ? body : body[..219] + "...";
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

public sealed class SpotifyRateLimitException : Exception
{
    public SpotifyRateLimitException(TimeSpan retryAfter, string endpoint)
        : base(BuildMessage(retryAfter, endpoint))
    {
        RetryAfter = retryAfter;
        Endpoint = endpoint;
    }

    public TimeSpan RetryAfter { get; }
    public string Endpoint { get; }

    private static string BuildMessage(TimeSpan retryAfter, string endpoint)
    {
        var wait = FormatDelay(retryAfter);
        if (retryAfter > TimeSpan.FromMinutes(15))
        {
            return $"Spotify ограничил запросы ({endpoint}) и прислал Retry-After около {wait}. Это необычно долго, автоповтор остановлен.";
        }

        return $"Spotify ограничил запросы ({endpoint}). Повтори через {wait}.";
    }

    private static string FormatDelay(TimeSpan delay)
    {
        if (delay >= TimeSpan.FromHours(1))
        {
            return $"{Math.Ceiling(delay.TotalHours):0} ч";
        }

        return delay >= TimeSpan.FromMinutes(1)
            ? $"{Math.Ceiling(delay.TotalMinutes):0} мин"
            : $"{Math.Ceiling(delay.TotalSeconds):0} сек";
    }
}
