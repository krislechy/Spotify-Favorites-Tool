using System.Collections.ObjectModel;

namespace SpotifyFavoritesTool;

public sealed record KaraokeLyricLine(TimeSpan Time, string Text);

public sealed class KaraokeLyrics
{
    public static KaraokeLyrics Empty { get; } = new(LyricsKind.None, Array.Empty<KaraokeLyricLine>(), null);

    public KaraokeLyrics(LyricsKind kind, IReadOnlyList<KaraokeLyricLine> lines, string? plainText)
    {
        Kind = kind;
        Lines = new ReadOnlyCollection<KaraokeLyricLine>(lines.ToArray());
        PlainText = plainText;
    }

    public LyricsKind Kind { get; }
    public IReadOnlyList<KaraokeLyricLine> Lines { get; }
    public string? PlainText { get; }
    public bool HasSyncedLines => Lines.Count > 0;
}

public enum LyricsKind
{
    None,
    Synced,
    Plain
}
