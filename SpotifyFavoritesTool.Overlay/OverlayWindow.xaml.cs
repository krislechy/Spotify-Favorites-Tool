using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Controls = System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace SpotifyFavoritesTool;

public partial class OverlayWindow : Window, IDisposable
{
    private const double CollapsedHeight = 150;
    private const double ExpandedHeight = 438;
    private const double MaxAlbumArtPreviewSize = 320;

    private readonly ObservableCollection<PlaybackTrack> _cachedTracks = [];
    private readonly Dictionary<string, BitmapImage> _albumArtCache = new(StringComparer.Ordinal);
    private string? _requestedAlbumImageUrl;
    private bool _isHistoryExpanded;
    private bool _disposed;

    public event EventHandler? FavoriteRequested;
    public event EventHandler? PreviousRequested;
    public event EventHandler? PlayPauseRequested;
    public event EventHandler? NextRequested;
    public event EventHandler<TrackRequestedEventArgs>? CachedTrackPlayRequested;
    public event EventHandler<TrackRequestedEventArgs>? CachedTrackFavoriteRequested;

    public OverlayWindow()
    {
        InitializeComponent();
        CachedTracksList.ItemsSource = _cachedTracks;
        ShowMessage("Overlay готов", "Жду текущий трек");
    }

    public void ShowTrack(PlaybackTrack track)
    {
        TrackTitle.Text = track.Name;
        ArtistText.Text = track.Artists;
        SetFavoriteState(track.IsLiked);
        SetPlaybackState(track.IsPlaying);
        _requestedAlbumImageUrl = track.AlbumImageUrl;
        _ = SetAlbumArtAsync(track.AlbumImageUrl);
    }

    public void ShowMessage(string title, string message)
    {
        TrackTitle.Text = title;
        ArtistText.Text = message;
        FavoriteText.Text = "Избранное недоступно";
        FavoriteButton.Content = "♡";
        SetPlaybackState(isPlaying: true);
        _requestedAlbumImageUrl = null;
        AlbumArt.Source = null;
        AlbumArtPreview.Source = null;
        AlbumArtPreviewToolTip.IsEnabled = false;
        ResetAlbumArtPreviewSize();
        AlbumPlaceholder.Visibility = Visibility.Visible;
    }

    public void SetCachedTracks(IEnumerable<PlaybackTrack> tracks)
    {
        _cachedTracks.Clear();
        foreach (var track in tracks)
        {
            _cachedTracks.Add(track);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AlbumArt.Source = null;
        _albumArtCache.Clear();
        _cachedTracks.Clear();
        FavoriteRequested = null;
        PreviousRequested = null;
        PlayPauseRequested = null;
        NextRequested = null;
        CachedTrackPlayRequested = null;
        CachedTrackFavoriteRequested = null;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PlaceNearTopRight();
        NativeMethods.ForceTopmost(new WindowInteropHelper(this).Handle);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        Dispose();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            e.Handled = true;
        }
    }

    private void OverlayRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
            // DragMove can throw if Windows has already ended the mouse capture.
        }
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        FavoriteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        PreviousRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        PlayPauseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        NextRequested?.Invoke(this, EventArgs.Empty);
    }

    private void HistoryToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _isHistoryExpanded = !_isHistoryExpanded;
        HistoryPanel.Visibility = _isHistoryExpanded ? Visibility.Visible : Visibility.Collapsed;
        HistoryRow.Height = _isHistoryExpanded ? new GridLength(288) : new GridLength(0);
        Height = _isHistoryExpanded ? ExpandedHeight : CollapsedHeight;
        HistoryToggleButton.ToolTip = _isHistoryExpanded ? "Скрыть треки из кеша" : "Показать треки из кеша";
    }

    private void CachedTrackPlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Controls.Button { Tag: PlaybackTrack track })
        {
            CachedTrackPlayRequested?.Invoke(this, new TrackRequestedEventArgs(track));
        }
    }

    private void CachedTrackFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Controls.Button { Tag: PlaybackTrack track })
        {
            CachedTrackFavoriteRequested?.Invoke(this, new TrackRequestedEventArgs(track));
        }
    }

    private void AlbumArt_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        AlbumArt.Source = null;
        AlbumPlaceholder.Visibility = Visibility.Visible;
    }

    private void SetFavoriteState(bool? isLiked)
    {
        if (isLiked == true)
        {
            FavoriteText.Text = "В Избранном";
            FavoriteButton.Content = "♥";
            return;
        }

        if (isLiked == false)
        {
            FavoriteText.Text = "Не в Избранном";
            FavoriteButton.Content = "♡";
            return;
        }

        FavoriteText.Text = "Статус Избранного неизвестен";
        FavoriteButton.Content = "♡";
    }

    private void SetPlaybackState(bool? isPlaying)
    {
        var showPause = isPlaying != false;
        PlayGlyph.Visibility = showPause ? Visibility.Collapsed : Visibility.Visible;
        PauseGlyphLeft.Visibility = showPause ? Visibility.Visible : Visibility.Collapsed;
        PauseGlyphRight.Visibility = showPause ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task SetAlbumArtAsync(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            AlbumArt.Source = null;
            AlbumArtPreview.Source = null;
            AlbumArtPreviewToolTip.IsEnabled = false;
            ResetAlbumArtPreviewSize();
            AlbumPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        if (_albumArtCache.TryGetValue(imageUrl, out var cachedImage))
        {
            SetAlbumArt(cachedImage);
            return;
        }

        try
        {
            var image = await AlbumArtLoader.LoadAsync(imageUrl);
            if (_disposed || image is null || !string.Equals(_requestedAlbumImageUrl, imageUrl, StringComparison.Ordinal))
            {
                return;
            }

            _albumArtCache[imageUrl] = image;
            SetAlbumArt(image);
        }
        catch
        {
            if (_disposed || !string.Equals(_requestedAlbumImageUrl, imageUrl, StringComparison.Ordinal))
            {
                return;
            }

            AlbumArt.Source = null;
            AlbumArtPreview.Source = null;
            AlbumArtPreviewToolTip.IsEnabled = false;
            ResetAlbumArtPreviewSize();
            AlbumPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void SetAlbumArt(BitmapImage image)
    {
        AlbumArt.Source = image;
        AlbumArtPreview.Source = image;
        AlbumArtPreviewToolTip.IsEnabled = true;
        SetAlbumArtPreviewSize(image);
        AlbumPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void SetAlbumArtPreviewSize(BitmapImage image)
    {
        AlbumArtPreviewFrame.Width = GetPreviewDimension(image.PixelWidth);
        AlbumArtPreviewFrame.Height = GetPreviewDimension(image.PixelHeight);
    }

    private void ResetAlbumArtPreviewSize()
    {
        AlbumArtPreviewFrame.Width = MaxAlbumArtPreviewSize;
        AlbumArtPreviewFrame.Height = MaxAlbumArtPreviewSize;
    }

    private static double GetPreviewDimension(int pixelSize)
    {
        return pixelSize <= 0 ? MaxAlbumArtPreviewSize : Math.Min(pixelSize, MaxAlbumArtPreviewSize);
    }

    private void PlaceNearTopRight()
    {
        var workArea = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        Left = workArea.Right - Width - 18;
        Top = workArea.Top + 18;
    }
}

public sealed class TrackRequestedEventArgs : EventArgs
{
    public TrackRequestedEventArgs(PlaybackTrack track)
    {
        Track = track;
    }

    public PlaybackTrack Track { get; }
}
