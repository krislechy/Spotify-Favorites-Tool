using System.Collections.ObjectModel;

namespace SpotifyFavoritesTool;

public sealed class ActivityLog
{
    private const int MaxEntries = 50;

    public ObservableCollection<ActivityLogEntry> Entries { get; } = new();

    public void Add(string title, string? details = null)
    {
        Entries.Insert(0, new ActivityLogEntry(DateTimeOffset.Now, title, details));

        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }
    }
}

public sealed record ActivityLogEntry(DateTimeOffset Time, string Title, string? Details)
{
    public string TimeText => Time.ToString("HH:mm:ss");
    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);
}
