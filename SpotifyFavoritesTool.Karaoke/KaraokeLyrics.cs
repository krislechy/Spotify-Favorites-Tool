using System.Collections.ObjectModel;

namespace SpotifyFavoritesTool;

public sealed record KaraokeLyricLine(TimeSpan Time, string Text);

public sealed class KaraokeLyrics
{
    public static KaraokeLyrics Empty { get; } = new(LyricsKind.None, Array.Empty<KaraokeLyricLine>(), null, null, null);

    public KaraokeLyrics(
        LyricsKind kind,
        IReadOnlyList<KaraokeLyricLine> lines,
        string? plainText,
        string? sourceName = null,
        string? sourceUrl = null)
    {
        Kind = kind;
        Lines = new ReadOnlyCollection<KaraokeLyricLine>(lines.ToArray());
        PlainText = plainText;
        SourceName = sourceName;
        SourceUrl = sourceUrl;
    }

    public LyricsKind Kind { get; }
    public IReadOnlyList<KaraokeLyricLine> Lines { get; }
    public string? PlainText { get; }
    public string? SourceName { get; }
    public string? SourceUrl { get; }
    public bool HasSyncedLines => Lines.Count > 0;
    public string? DisplayText => !string.IsNullOrWhiteSpace(PlainText)
        ? PlainText
        : Lines.Count > 0
            ? string.Join(Environment.NewLine, Lines.Select(line => line.Text))
            : null;
}

public enum LyricsKind
{
    None,
    Synced,
    Plain,
    Link
}
