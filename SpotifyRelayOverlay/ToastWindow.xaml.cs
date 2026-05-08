using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SpotifyRelayOverlay;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _closeTimer = new() { Interval = TimeSpan.FromSeconds(2.8) };

    public ToastWindow(PlaybackSnapshot snapshot, string action, string? extra = null)
    {
        InitializeComponent();
        _closeTimer.Tick += (_, _) => Close();

        ActionText.Text = action;
        ExtraText.Text = extra ?? string.Empty;

        if (snapshot.Track is null)
        {
            TrackTitle.Text = "Spotify";
            ArtistText.Text = snapshot.Status;
            return;
        }

        TrackTitle.Text = snapshot.Track.Name;
        ArtistText.Text = snapshot.Track.Artists;
        SetAlbumArt(snapshot.Track.AlbumImageUrl);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.WorkArea.Left + 18;
        Top = SystemParameters.WorkArea.Top + 18;
        _closeTimer.Start();
    }

    private void SetAlbumArt(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            AlbumArt.Source = null;
            AlbumPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(imageUrl, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            AlbumArt.Source = image;
            AlbumPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            AlbumArt.Source = null;
            AlbumPlaceholder.Visibility = Visibility.Visible;
        }
    }
}
