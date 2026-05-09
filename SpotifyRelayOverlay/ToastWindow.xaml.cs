using System.Windows;
using System.Windows.Interop;
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
        SetAlbumArt(result.Track.AlbumImageUrl);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + 18;
        Top = workArea.Top + 18;

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.ForceTopmost(hwnd);

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
