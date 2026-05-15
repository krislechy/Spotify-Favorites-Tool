namespace SpotifyFavoritesTool;

public sealed record PlaybackTrack(
    string Id,
    string Uri,
    string Name,
    string Artists,
    string? AlbumImageUrl,
    string? ContextUri = null,
    bool? IsLiked = null,
    bool? IsPlaying = null,
    int? DurationMs = null)
{
    public string DisplayLine => $"{Name} · {Artists}";
    public string DurationText => DurationMs is > 0 ? FormatDuration(DurationMs.Value) : string.Empty;
    public string FavoriteGlyph => IsLiked == true ? "♥" : "♡";
    public string NowPlayingText => IsPlaying == true ? "сейчас играет" : string.Empty;

    public PlaybackTrack WithFavoriteStatus(bool isLiked)
    {
        return this with { IsLiked = isLiked };
    }

    public PlaybackTrack WithPlaybackState(bool? isPlaying)
    {
        return this with { IsPlaying = isPlaying };
    }

    private static string FormatDuration(int durationMs)
    {
        var duration = TimeSpan.FromMilliseconds(durationMs);
        return duration.Hours > 0
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes}:{duration.Seconds:00}";
    }
}

public sealed record FavoriteToggleResult(
    PlaybackTrack Track,
    string Message,
    FavoriteStatusSource PreviousStatusSource)
{
    public bool IsLiked => Track.IsLiked == true;
}

public sealed record FavoriteStatusResult(
    PlaybackTrack Track,
    FavoriteStatusSource Source);

public enum FavoriteStatusSource
{
    Cache,
    SpotifyApi
}
