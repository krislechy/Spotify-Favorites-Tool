using System.Net;

namespace SpotifyFavoritesTool;

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
