namespace SpotifyFavoritesTool;

public sealed class TrackFavoriteCache
{
    private readonly Dictionary<string, PlaybackTrack> _tracks = new(StringComparer.Ordinal);
    private readonly List<string> _trackOrder = [];

    public void Clear()
    {
        _tracks.Clear();
        _trackOrder.Clear();
    }

    public PlaybackTrack Store(PlaybackTrack track)
    {
        var storedTrack = _tracks.TryGetValue(track.Uri, out var existingTrack)
            ? Merge(existingTrack, track)
            : track;

        if (storedTrack.IsPlaying == true)
        {
            MarkOtherTracksAsNotPlaying(storedTrack.Uri);
        }

        _tracks[track.Uri] = storedTrack;
        _trackOrder.Remove(track.Uri);
        _trackOrder.Insert(0, track.Uri);
        return storedTrack;
    }

    public bool TryGetWithFavoriteStatus(PlaybackTrack track, out PlaybackTrack cachedTrack)
    {
        if (_tracks.TryGetValue(track.Uri, out var storedTrack) && storedTrack.IsLiked.HasValue)
        {
            cachedTrack = Merge(storedTrack, track).WithFavoriteStatus(storedTrack.IsLiked.Value);
            return true;
        }

        cachedTrack = null!;
        return false;
    }

    public IReadOnlyList<PlaybackTrack> GetTracks()
    {
        return _trackOrder
            .Where(_tracks.ContainsKey)
            .Select(uri => _tracks[uri])
            .ToArray();
    }

    public IReadOnlyList<PlaybackTrack> EnrichTracks(IEnumerable<PlaybackTrack> tracks, PlaybackTrack? currentTrack)
    {
        return tracks
            .Select(track => EnrichTrack(track, currentTrack))
            .ToArray();
    }

    private PlaybackTrack EnrichTrack(PlaybackTrack track, PlaybackTrack? currentTrack)
    {
        var enrichedTrack = _tracks.TryGetValue(track.Uri, out var storedTrack)
            ? Merge(storedTrack, track)
            : track;

        if (currentTrack is not null && string.Equals(track.Uri, currentTrack.Uri, StringComparison.Ordinal))
        {
            enrichedTrack = Merge(enrichedTrack, currentTrack).WithPlaybackState(currentTrack.IsPlaying);
        }
        else if (currentTrack is not null)
        {
            enrichedTrack = enrichedTrack.WithPlaybackState(false);
        }

        return enrichedTrack;
    }

    private static PlaybackTrack Merge(PlaybackTrack storedTrack, PlaybackTrack latestTrack)
    {
        return latestTrack with
        {
            Name = string.IsNullOrWhiteSpace(latestTrack.Name) ? storedTrack.Name : latestTrack.Name,
            Artists = string.IsNullOrWhiteSpace(latestTrack.Artists) ? storedTrack.Artists : latestTrack.Artists,
            AlbumImageUrl = string.IsNullOrWhiteSpace(latestTrack.AlbumImageUrl) ? storedTrack.AlbumImageUrl : latestTrack.AlbumImageUrl,
            ContextUri = string.IsNullOrWhiteSpace(latestTrack.ContextUri) ? storedTrack.ContextUri : latestTrack.ContextUri,
            IsLiked = latestTrack.IsLiked ?? storedTrack.IsLiked,
            IsPlaying = latestTrack.IsPlaying ?? storedTrack.IsPlaying
        };
    }

    private void MarkOtherTracksAsNotPlaying(string currentTrackUri)
    {
        foreach (var uri in _trackOrder)
        {
            if (string.Equals(uri, currentTrackUri, StringComparison.Ordinal))
            {
                continue;
            }

            if (_tracks.TryGetValue(uri, out var track) && track.IsPlaying == true)
            {
                _tracks[uri] = track.WithPlaybackState(false);
            }
        }
    }
}
