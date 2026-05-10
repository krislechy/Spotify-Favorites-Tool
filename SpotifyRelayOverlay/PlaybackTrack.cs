namespace SpotifyRelayOverlay;

public sealed record PlaybackTrack(
    string Id,
    string Uri,
    string Name,
    string Artists,
    string? AlbumImageUrl,
    bool? IsLiked = null)
{
    public PlaybackTrack WithFavoriteStatus(bool isLiked)
    {
        return this with { IsLiked = isLiked };
    }
}

public sealed record FavoriteToggleResult(
    PlaybackTrack Track,
    string Message)
{
    public bool IsLiked => Track.IsLiked == true;
}
