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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SpotifyAuthService _auth;
    private readonly SemaphoreSlim _requestGate = new(1, 1);

    public SpotifyClient(SpotifyAuthService auth)
    {
        _auth = auth;
    }

    public async Task<FavoriteToggleResult> ToggleCurrentTrackFavoriteAsync(CancellationToken cancellationToken = default)
    {
        var track = await GetCurrentTrackAsync(cancellationToken);
        var isLiked = await IsTrackLikedAsync(track.Id, cancellationToken);
        var nextLiked = !isLiked;

        await SetTrackLikedAsync(track.Id, nextLiked, cancellationToken);
        var message = nextLiked ? "Добавлено в избраное" : "Удалено из избранного";
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

    private async Task<bool> IsTrackLikedAsync(string trackId, CancellationToken cancellationToken)
    {
        var id = Uri.EscapeDataString(trackId);
        var response = await SendAsync(HttpMethod.Get, $"{ApiRoot}/me/tracks/contains?ids={id}", cancellationToken);
        var values = JsonSerializer.Deserialize<bool[]>(response.Body, JsonOptions);
        return values is { Length: > 0 } && values[0];
    }

    private async Task SetTrackLikedAsync(string trackId, bool isLiked, CancellationToken cancellationToken)
    {
        var id = Uri.EscapeDataString(trackId);
        var method = isLiked ? HttpMethod.Put : HttpMethod.Delete;
        await SendAsync(method, $"{ApiRoot}/me/tracks?ids={id}", cancellationToken);
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
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
            {
                throw CreateApiException(response.StatusCode, body);
            }

            return new ApiResponse(response.StatusCode, body);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private static Exception CreateApiException(HttpStatusCode statusCode, string body)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => new InvalidOperationException(
                "Spotify вернул 401. Открой настройки, нажми «Выйти», потом «Войти в Spotify» и выдай новые права."),
            HttpStatusCode.Forbidden => new InvalidOperationException(
                "Spotify отказал в доступе. Заново войди в настройках для прав на чтение и изменение избранных треков."),
            (HttpStatusCode)429 => new InvalidOperationException(
                "Spotify временно ограничил запросы. Подожди немного перед следующим нажатием."),
            _ => new SpotifyApiException(statusCode, body)
        };
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
