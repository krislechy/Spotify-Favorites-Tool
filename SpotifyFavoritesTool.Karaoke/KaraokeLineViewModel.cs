using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SpotifyFavoritesTool;

public sealed class KaraokeLineViewModel : INotifyPropertyChanged
{
    private bool _isCurrent;

    public KaraokeLineViewModel(KaraokeLyricLine line)
    {
        Line = line;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public KaraokeLyricLine Line { get; }
    public string TimeText => $"{(int)Line.Time.TotalMinutes}:{Line.Time.Seconds:00}";
    public string Text => Line.Text;

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent == value)
            {
                return;
            }

            _isCurrent = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
