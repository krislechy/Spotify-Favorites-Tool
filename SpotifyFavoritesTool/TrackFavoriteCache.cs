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

    private static PlaybackTrack Merge(PlaybackTrack storedTrack, PlaybackTrack latestTrack)
    {
        return latestTrack with
        {
            Name = string.IsNullOrWhiteSpace(latestTrack.Name) ? storedTrack.Name : latestTrack.Name,
            Artists = string.IsNullOrWhiteSpace(latestTrack.Artists) ? storedTrack.Artists : latestTrack.Artists,
            AlbumImageUrl = string.IsNullOrWhiteSpace(latestTrack.AlbumImageUrl) ? storedTrack.AlbumImageUrl : latestTrack.AlbumImageUrl,
            IsLiked = latestTrack.IsLiked ?? storedTrack.IsLiked,
            IsPlaying = latestTrack.IsPlaying ?? storedTrack.IsPlaying
        };
    }
}
