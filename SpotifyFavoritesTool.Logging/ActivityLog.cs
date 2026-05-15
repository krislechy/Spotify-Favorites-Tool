using System.Collections.ObjectModel;

namespace SpotifyFavoritesTool;

public sealed class ActivityLog
{
    private const int MaxEntries = 50;

    public ObservableCollection<ActivityLogEntry> Entries { get; } = new();

    public void Add(string message)
    {
        Entries.Insert(0, new ActivityLogEntry(DateTimeOffset.Now, message));

        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }
    }
}

public sealed record ActivityLogEntry(DateTimeOffset Time, string Message)
{
    public string TimeText => Time.ToString("HH:mm:ss");
}
