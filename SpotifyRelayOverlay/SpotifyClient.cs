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
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private string? _lastTrackUri;
    private bool _lastTrackLiked;

    public SpotifyClient(SpotifyAuthService auth)
    {
        _auth = auth;
    }

    public async Task<FavoriteToggleResult> ToggleCurrentTrackFavoriteAsync(CancellationToken cancellationToken = default)
    {
        var track = await GetCurrentTrackOrNullAsync(cancellationToken)
            ?? throw new InvalidOperationException("Spotify сейчас ничего не играет.");
        var isLiked = await GetLikedStateAsync(track, cancellationToken);
        var nextLiked = !isLiked;

        await SetTrackLikedAsync(track, nextLiked, cancellationToken);
        var updatedTrack = track.WithFavoriteStatus(nextLiked);
        _lastTrackUri = updatedTrack.Uri;
        _lastTrackLiked = nextLiked;

        var message = nextLiked ? "Добавлено в Избранное" : "Убрано из Избранного";
        return new FavoriteToggleResult(updatedTrack, message);
    }

    public async Task<PlaybackTrack?> GetCurrentTrackOrNullAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            HttpMethod.Get,
            $"{ApiRoot}/me/player/currently-playing?additional_types=track",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
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

    public async Task<bool> GetTrackLikedStateAsync(PlaybackTrack track, CancellationToken cancellationToken = default)
    {
        return await GetLikedStateAsync(track, cancellationToken);
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

            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == (HttpStatusCode)429)
            {
                throw new SpotifyRateLimitException(GetRetryAfter(response), DescribeEndpoint(url), body);
            }

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
            {
                throw CreateApiException(response.StatusCode, body, url);
            }

            return new ApiResponse(response.StatusCode, body);
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
                $"{BuildScopeError()} Endpoint: {DescribeEndpoint(url)}. Ответ Spotify: {FormatBody(body)}"),
            _ => new SpotifyApiException(statusCode, body)
        };
    }

    private static string BuildScopeError()
    {
        return $"Spotify вернул 403 Forbidden. Для Избранного нужны права {SpotifyAuthService.RequiredScopes}. " +
            "Открой настройки, нажми «Выйти», затем «Войти в Spotify» и подтверди доступ.";
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }

        return null;
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

    private static string FormatBody(string body)
    {
        return string.IsNullOrWhiteSpace(body) ? "пустой ответ" : body;
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
    public SpotifyRateLimitException(TimeSpan? retryAfter, string endpoint, string body)
        : base(BuildMessage(retryAfter, endpoint, body))
    {
        RetryAfter = retryAfter;
        Endpoint = endpoint;
        Body = body;
    }

    public TimeSpan? RetryAfter { get; }
    public string Endpoint { get; }
    public string Body { get; }

    private static string BuildMessage(TimeSpan? retryAfter, string endpoint, string body)
    {
        var message = $"Spotify вернул 429 ({endpoint}).";

        if (retryAfter is { } delay)
        {
            message += Environment.NewLine + $"Reset after: {FormatDelay(delay)}.";
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            message += Environment.NewLine + $"Детали: {body}";
        }

        return message;
    }

    private static string FormatDelay(TimeSpan delay)
    {
        var parts = new List<string>();

        if (delay.Hours > 0)
        {
            parts.Add($"{delay.Hours} ч");
        }

        if (delay.Minutes > 0)
        {
            parts.Add($"{delay.Minutes} мин");
        }

        if (delay.Seconds > 0 || parts.Count == 0)
        {
            parts.Add($"{delay.Seconds} сек");
        }

        return string.Join(' ', parts);
    }
}
