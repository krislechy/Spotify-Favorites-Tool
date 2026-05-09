using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpotifyRelayOverlay;

public partial class ToastWindow : Window
{
    public ToastWindow(FavoriteToggleResult result)
    {
        InitializeComponent();

        ActionText.Text = result.Message;
        TrackTitle.Text = result.Track.Name;
        ArtistText.Text = result.Track.Artists;
        HeartText.Text = result.IsLiked ? "♥" : "♡";
        HeartText.Foreground = result.IsLiked
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 215, 96))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(213, 231, 220));
        SetAlbumArt(result.Track.AlbumImageUrl);
    }

    public ToastWindow(string title, string message)
    {
        InitializeComponent();

        ActionText.Text = title;
        TrackTitle.Text = message;
        ArtistText.Text = "Spotify Избранное";
        HeartText.Text = "!";
        HeartText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 196, 87));
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + 18;
        Top = workArea.Top + 18;

        NativeMethods.ForceTopmost(new WindowInteropHelper(this).Handle);

        await Task.Delay(TimeSpan.FromSeconds(3.2));
        if (IsVisible)
        {
            Close();
        }
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
