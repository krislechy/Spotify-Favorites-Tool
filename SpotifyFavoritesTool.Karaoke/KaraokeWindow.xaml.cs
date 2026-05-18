using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SpotifyFavoritesTool;

public partial class KaraokeWindow : Window
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMilliseconds(220);

    private readonly SpotifyClient _spotify;
    private readonly LrclibLyricsService _lyricsService = new();
    private readonly ObservableCollection<KaraokeLineViewModel> _lines = new();
    private readonly DispatcherTimer _syncTimer;
    private readonly Stopwatch _positionClock = new();

    private CancellationTokenSource? _loadCancellation;
    private PlaybackTrack? _track;
    private TimeSpan _basePosition;
    private int _currentLineIndex = -1;

    public KaraokeWindow(SpotifyClient spotify)
    {
        InitializeComponent();
        _spotify = spotify;
        LyricsList.ItemsSource = _lines;
        _syncTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = SyncInterval
        };
        _syncTimer.Tick += SyncTimer_Tick;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCurrentTrackAsync();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _syncTimer.Stop();
        _syncTimer.Tick -= SyncTimer_Tick;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadCurrentTrackAsync();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            e.Handled = true;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Windows can end capture before DragMove starts.
        }
    }

    private async Task LoadCurrentTrackAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        try
        {
            SetLoadingState();
            var track = await _spotify.GetCurrentTrackOrNullAsync(cancellationToken);
            if (track is null)
            {
                ShowNoTrack();
                return;
            }

            _track = track;
            ShowTrack(track);

            var lyrics = await _lyricsService.GetLyricsAsync(track, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ShowLyrics(track, lyrics);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void SetLoadingState()
    {
        _syncTimer.Stop();
        WindowStatusText.Text = "Загрузка";
        SyncStatusText.Text = "Получаю текущий трек и текст";
        PlainLyricsText.Visibility = Visibility.Collapsed;
        LyricsList.Visibility = Visibility.Visible;
        _lines.Clear();
    }

    private void ShowNoTrack()
    {
        _syncTimer.Stop();
        _track = null;
        TrackTitle.Text = "Ничего не играет";
        ArtistText.Text = string.Empty;
        SyncStatusText.Text = "Включи трек в Spotify и нажми обновить.";
        WindowStatusText.Text = "Ожидание Spotify";
        AlbumArt.Source = null;
        AlbumPlaceholder.Visibility = Visibility.Visible;
        _lines.Clear();
        PlainLyricsText.Visibility = Visibility.Collapsed;
        LyricsList.Visibility = Visibility.Visible;
    }

    private async void ShowTrack(PlaybackTrack track)
    {
        TrackTitle.Text = track.Name;
        ArtistText.Text = track.Artists;
        WindowStatusText.Text = track.IsPlaying == true ? "Синхронизация с текущим треком" : "Трек на паузе";
        SyncStatusText.Text = "Ищу синхронизированный текст";
        await LoadArtworkAsync(track);
    }

    private async Task LoadArtworkAsync(PlaybackTrack track)
    {
        try
        {
            var image = await AlbumArtLoader.LoadAsync(track.AlbumImageUrl);
            if (!string.Equals(_track?.Uri, track.Uri, StringComparison.Ordinal))
            {
                return;
            }

            AlbumArt.Source = image;
            AlbumPlaceholder.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            AlbumArt.Source = null;
            AlbumPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void ShowLyrics(PlaybackTrack track, KaraokeLyrics lyrics)
    {
        _syncTimer.Stop();
        _currentLineIndex = -1;
        _lines.Clear();
        PlainLyricsText.Visibility = Visibility.Collapsed;
        LyricsList.Visibility = Visibility.Visible;

        if (lyrics.HasSyncedLines)
        {
            foreach (var line in lyrics.Lines)
            {
                _lines.Add(new KaraokeLineViewModel(line));
            }

            _basePosition = TimeSpan.FromMilliseconds(Math.Max(0, track.ProgressMs ?? 0));
            RestartClock(track.IsPlaying == true);
            SyncStatusText.Text = "Синхронизированный текст найден";
            _syncTimer.Start();
            UpdateCurrentLine();
            return;
        }

        if (lyrics.Kind == LyricsKind.Plain && !string.IsNullOrWhiteSpace(lyrics.PlainText))
        {
            PlainLyricsText.Text = lyrics.PlainText;
            PlainLyricsText.Visibility = Visibility.Visible;
            LyricsList.Visibility = Visibility.Collapsed;
            SyncStatusText.Text = "Есть только обычный текст без таймингов";
            return;
        }

        SyncStatusText.Text = "Текст для этого трека не найден.";
    }

    private void RestartClock(bool isPlaying)
    {
        _positionClock.Reset();
        if (isPlaying)
        {
            _positionClock.Start();
        }
    }

    private void SyncTimer_Tick(object? sender, EventArgs e)
    {
        UpdateCurrentLine();
    }

    private void UpdateCurrentLine()
    {
        if (_lines.Count == 0)
        {
            return;
        }

        var position = _basePosition + (_positionClock.IsRunning ? _positionClock.Elapsed : TimeSpan.Zero);
        var nextIndex = 0;
        for (var index = 0; index < _lines.Count; index++)
        {
            if (_lines[index].Line.Time > position)
            {
                break;
            }

            nextIndex = index;
        }

        if (nextIndex == _currentLineIndex)
        {
            return;
        }

        if (_currentLineIndex >= 0 && _currentLineIndex < _lines.Count)
        {
            _lines[_currentLineIndex].IsCurrent = false;
        }

        _currentLineIndex = nextIndex;
        _lines[_currentLineIndex].IsCurrent = true;
        LyricsList.ScrollIntoView(_lines[_currentLineIndex]);
    }

    private void ShowError(string details)
    {
        _syncTimer.Stop();
        WindowStatusText.Text = "Ошибка";
        SyncStatusText.Text = "Не удалось загрузить караоке. Подробность записана в журнал приложения.";
        _lines.Clear();
        PlainLyricsText.Visibility = Visibility.Collapsed;
        LyricsList.Visibility = Visibility.Visible;
        _lines.Add(new KaraokeLineViewModel(new KaraokeLyricLine(TimeSpan.Zero, details)));
    }
}
