namespace SpotifyFavoritesTool;

public sealed class TrackFavoriteCache
{
    private readonly Dictionary<string, PlaybackTrack> _tracks = new(StringComparer.Ordinal);

    public void Clear()
    {
        _tracks.Clear();
    }

    public PlaybackTrack Store(PlaybackTrack track)
    {
        _tracks[track.Uri] = track;
        return track;
    }

    public bool TryGetWithFavoriteStatus(PlaybackTrack track, out PlaybackTrack cachedTrack)
    {
        if (_tracks.TryGetValue(track.Uri, out var storedTrack) && storedTrack.IsLiked.HasValue)
        {
            cachedTrack = track.WithFavoriteStatus(storedTrack.IsLiked.Value);
            return true;
        }

        cachedTrack = null!;
        return false;
    }
}
