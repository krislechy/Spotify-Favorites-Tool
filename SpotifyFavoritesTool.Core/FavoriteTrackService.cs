namespace SpotifyFavoritesTool;

public sealed class FavoriteTrackService
{
    private readonly SpotifyClient _spotify;
    private readonly TrackFavoriteCache _cache = new();
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
        var queueTracks = await _spotify.GetQueueTracksAsync(currentTrack, cancellationToken);
        var recentlyPlayedTracks = await _spotify.GetRecentlyPlayedTracksAsync(cancellationToken);
        var streamTracks = BuildPlaybackStream(currentTrack, queueTracks, recentlyPlayedTracks);

        return new OverlayTrackList(
            "Очередь Spotify",
            _cache.EnrichTracks(streamTracks, currentTrack),
            IsPlaybackContext: true,
            EmptyMessage: "Spotify не отдал текущую очередь.");
    }

    public void ResetObservation()
    {
        _lastObservedTrackUri = null;
        LastObservedTrack = null;
    }

    public void ClearCache()
    {
        _cache.Clear();
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

    private static IReadOnlyList<PlaybackTrack> BuildPlaybackStream(
        PlaybackTrack? currentTrack,
        IReadOnlyList<PlaybackTrack> queueTracks,
        IReadOnlyList<PlaybackTrack> recentlyPlayedTracks)
    {
        var tracks = new List<PlaybackTrack>();
        if (currentTrack is not null)
        {
            tracks.Add(currentTrack);
        }

        tracks.AddRange(queueTracks);
        tracks.AddRange(recentlyPlayedTracks);

        var result = new List<PlaybackTrack>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var track in tracks)
        {
            if (seen.Add(track.Uri))
            {
                result.Add(track);
            }
        }

        return result;
    }
}
