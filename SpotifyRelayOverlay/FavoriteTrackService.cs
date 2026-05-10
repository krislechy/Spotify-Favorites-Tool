namespace SpotifyRelayOverlay;

public sealed class FavoriteTrackService
{
    private readonly SpotifyClient _spotify;
    private readonly TrackFavoriteCache _cache = new();
    private string? _lastObservedTrackUri;

    public FavoriteTrackService(SpotifyClient spotify)
    {
        _spotify = spotify;
    }

    public void ResetObservation()
    {
        _lastObservedTrackUri = null;
    }

    public void ClearCache()
    {
        _cache.Clear();
        ResetObservation();
    }

    public async Task<FavoriteToggleResult> ToggleCurrentTrackAsync()
    {
        var track = await GetCurrentTrackOrThrowAsync();
        var trackWithStatus = await GetTrackWithFavoriteStatusAsync(track);
        var nextLiked = trackWithStatus.IsLiked != true;

        await _spotify.SetTrackFavoriteAsync(trackWithStatus, nextLiked);

        var updatedTrack = trackWithStatus.WithFavoriteStatus(nextLiked);
        CacheObservedTrack(updatedTrack);

        var message = nextLiked ? "Добавлено в Избранное" : "Убрано из Избранного";
        return new FavoriteToggleResult(updatedTrack, message);
    }

    public async Task<PlaybackTrack?> GetCurrentTrackWithFavoriteStatusAsync()
    {
        var track = await _spotify.GetCurrentTrackOrNullAsync();
        if (track is null)
        {
            return null;
        }

        return await GetTrackWithFavoriteStatusAsync(track);
    }

    public async Task<PlaybackTrack?> GetChangedTrackWithFavoriteStatusAsync()
    {
        var track = await _spotify.GetCurrentTrackOrNullAsync();
        if (track is null)
        {
            ResetObservation();
            return null;
        }

        if (string.Equals(_lastObservedTrackUri, track.Uri, StringComparison.Ordinal))
        {
            return null;
        }

        return await GetTrackWithFavoriteStatusAsync(track);
    }

    private async Task<PlaybackTrack> GetCurrentTrackOrThrowAsync()
    {
        return await _spotify.GetCurrentTrackOrNullAsync()
            ?? throw new InvalidOperationException("Spotify сейчас ничего не играет.");
    }

    private async Task<PlaybackTrack> GetTrackWithFavoriteStatusAsync(PlaybackTrack track)
    {
        if (_cache.TryGetWithFavoriteStatus(track, out var cachedTrack))
        {
            return CacheObservedTrack(cachedTrack);
        }

        var isLiked = await _spotify.GetTrackLikedStateAsync(track);
        return CacheObservedTrack(track.WithFavoriteStatus(isLiked));
    }

    private PlaybackTrack CacheObservedTrack(PlaybackTrack track)
    {
        _lastObservedTrackUri = track.Uri;
        return _cache.Store(track);
    }
}
