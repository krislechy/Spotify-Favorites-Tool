namespace SpotifyFavoritesTool;

public sealed class FavoriteTrackService
{
    private readonly SpotifyClient _spotify;
    private readonly TrackFavoriteCache _cache = new();
    private readonly Dictionary<string, IReadOnlyList<PlaybackTrack>> _contextTrackCache = new(StringComparer.Ordinal);
    private string? _lastObservedTrackUri;

    public FavoriteTrackService(SpotifyClient spotify)
    {
        _spotify = spotify;
    }

    public PlaybackTrack? LastObservedTrack { get; private set; }
    public IReadOnlyList<PlaybackTrack> CachedTracks => _cache.GetTracks();

    public async Task<OverlayTrackList> GetOverlayTrackListAsync(CancellationToken cancellationToken = default)
    {
        var currentTrack = LastObservedTrack;
        var contextUri = currentTrack?.ContextUri;
        if (SpotifyClient.IsPlaylistContext(contextUri))
        {
            var contextTracks = await GetContextTracksAsync(contextUri!, cancellationToken);
            return new OverlayTrackList(
                "Текущий плейлист",
                _cache.EnrichTracks(contextTracks, currentTrack),
                IsPlaybackContext: true);
        }

        return new OverlayTrackList(
            "Воспроизводилось ранее",
            _cache.EnrichTracks(CachedTracks, currentTrack),
            IsPlaybackContext: false);
    }

    public void ResetObservation()
    {
        _lastObservedTrackUri = null;
        LastObservedTrack = null;
    }

    public void ClearCache()
    {
        _cache.Clear();
        _contextTrackCache.Clear();
        ResetObservation();
    }

    public PlaybackTrack StoreObservedTrack(PlaybackTrack track)
    {
        return CacheObservedTrack(track);
    }

    public async Task<FavoriteToggleResult> ToggleCurrentTrackAsync()
    {
        var track = await GetCurrentTrackOrThrowAsync();
        return await ToggleTrackAsync(track, updateObservation: true);
    }

    public async Task<FavoriteToggleResult> ToggleCachedTrackAsync(PlaybackTrack track)
    {
        return await ToggleTrackAsync(track, updateObservation: false);
    }

    private async Task<FavoriteToggleResult> ToggleTrackAsync(PlaybackTrack track, bool updateObservation)
    {
        var currentStatus = await GetTrackWithFavoriteStatusAsync(track, updateObservation);
        var nextLiked = currentStatus.Track.IsLiked != true;

        await _spotify.SetTrackFavoriteAsync(currentStatus.Track, nextLiked);

        var updatedTrack = currentStatus.Track.WithFavoriteStatus(nextLiked);
        updatedTrack = CacheTrack(updatedTrack, updateObservation);

        var message = nextLiked ? "Добавлено в Избранное" : "Убрано из Избранного";
        return new FavoriteToggleResult(updatedTrack, message, currentStatus.Source);
    }

    public async Task<FavoriteStatusResult?> GetCurrentTrackWithFavoriteStatusAsync()
    {
        var track = await _spotify.GetCurrentTrackOrNullAsync();
        if (track is null)
        {
            return null;
        }

        return await GetTrackWithFavoriteStatusAsync(track, updateObservation: true);
    }

    public async Task<FavoriteStatusResult?> GetChangedTrackWithFavoriteStatusAsync()
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

        return await GetTrackWithFavoriteStatusAsync(track, updateObservation: true);
    }

    private async Task<PlaybackTrack> GetCurrentTrackOrThrowAsync()
    {
        return await _spotify.GetCurrentTrackOrNullAsync()
            ?? throw new InvalidOperationException("Spotify сейчас ничего не играет.");
    }

    private async Task<FavoriteStatusResult> GetTrackWithFavoriteStatusAsync(PlaybackTrack track, bool updateObservation)
    {
        if (_cache.TryGetWithFavoriteStatus(track, out var cachedTrack))
        {
            return new FavoriteStatusResult(CacheTrack(cachedTrack, updateObservation), FavoriteStatusSource.Cache);
        }

        var isLiked = await _spotify.GetTrackLikedStateAsync(track);
        return new FavoriteStatusResult(
            CacheTrack(track.WithFavoriteStatus(isLiked), updateObservation),
            FavoriteStatusSource.SpotifyApi);
    }

    private PlaybackTrack CacheObservedTrack(PlaybackTrack track)
    {
        _lastObservedTrackUri = track.Uri;
        return CacheTrack(track, updateObservation: true);
    }

    private PlaybackTrack CacheTrack(PlaybackTrack track, bool updateObservation)
    {
        var cachedTrack = _cache.Store(track);
        if (updateObservation || string.Equals(LastObservedTrack?.Uri, cachedTrack.Uri, StringComparison.Ordinal))
        {
            LastObservedTrack = cachedTrack;
        }

        return cachedTrack;
    }

    private async Task<IReadOnlyList<PlaybackTrack>> GetContextTracksAsync(string contextUri, CancellationToken cancellationToken)
    {
        if (_contextTrackCache.TryGetValue(contextUri, out var cachedTracks))
        {
            return cachedTracks;
        }

        var tracks = await _spotify.GetPlaylistTracksAsync(contextUri, cancellationToken);
        _contextTrackCache[contextUri] = tracks;
        return tracks;
    }
}
