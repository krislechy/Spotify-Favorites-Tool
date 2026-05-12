namespace SpotifyFavoritesTool;

public sealed record PlaybackTrack(
    string Id,
    string Uri,
    string Name,
    string Artists,
    string? AlbumImageUrl,
    bool? IsLiked = null,
    bool? IsPlaying = null)
{
    public PlaybackTrack WithFavoriteStatus(bool isLiked)
    {
        return this with { IsLiked = isLiked };
    }

    public PlaybackTrack WithPlaybackState(bool? isPlaying)
    {
        return this with { IsPlaying = isPlaying };
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
