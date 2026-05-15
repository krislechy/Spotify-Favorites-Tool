using System.Text;

namespace SpotifyFavoritesTool;

public static class CriticalBugLog
{
    private const string AppFolderName = "SpotifyFavoritesTool";
    private const string FileName = "critical.log";

    private static readonly object SyncRoot = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName,
        FileName);

    public static void Write(string source, Exception exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entry = BuildEntry(source, exception);
            lock (SyncRoot)
            {
                File.AppendAllText(FilePath, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Last-resort logging must never crash the app while handling a crash.
        }
    }

    private static string BuildEntry(string source, Exception exception)
    {
        var builder = new StringBuilder()
            .AppendLine("============================================================")
            .AppendLine($"Time: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}")
            .AppendLine($"Source: {source}")
            .AppendLine($"Exception: {exception.GetType().FullName}")
            .AppendLine($"Message: {exception.Message}")
            .AppendLine("StackTrace:")
            .AppendLine(exception.ToString())
            .AppendLine();

        return builder.ToString();
    }
}
