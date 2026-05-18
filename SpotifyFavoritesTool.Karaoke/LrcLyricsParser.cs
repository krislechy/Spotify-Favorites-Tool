using System.Globalization;
using System.Text.RegularExpressions;

namespace SpotifyFavoritesTool;

public static partial class LrcLyricsParser
{
    public static IReadOnlyList<KaraokeLyricLine> Parse(string? syncedLyrics)
    {
        if (string.IsNullOrWhiteSpace(syncedLyrics))
        {
            return Array.Empty<KaraokeLyricLine>();
        }

        var lines = new List<KaraokeLyricLine>();
        foreach (var rawLine in syncedLyrics.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var matches = TimestampRegex().Matches(rawLine);
            if (matches.Count == 0)
            {
                continue;
            }

            var text = TimestampRegex().Replace(rawLine, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in matches)
            {
                if (TryParseTimestamp(match.Groups["time"].Value, out var time))
                {
                    lines.Add(new KaraokeLyricLine(time, text));
                }
            }
        }

        return lines
            .OrderBy(line => line.Time)
            .ToArray();
    }

    private static bool TryParseTimestamp(string value, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        var parts = value.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return false;
        }

        if (!double.TryParse(parts[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds))
        {
            return false;
        }

        time = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        return true;
    }

    [GeneratedRegex(@"\[(?<time>\d{1,3}:\d{2}(?:\.\d{1,3})?)\]")]
    private static partial Regex TimestampRegex();
}
