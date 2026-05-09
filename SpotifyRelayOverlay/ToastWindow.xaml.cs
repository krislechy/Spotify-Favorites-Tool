using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpotifyRelayOverlay;

public partial class ToastWindow : Window
{
    private readonly TimeSpan _visibleDuration = TimeSpan.FromSeconds(3.2);

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
        ConfigureErrorLayout();

        ActionText.Text = title;
        TrackTitle.Text = message;
        ArtistText.Text = string.Empty;
        HeartText.Text = "!";
        HeartText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 196, 87));
        _visibleDuration = TimeSpan.FromSeconds(9);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + 18;
        Top = workArea.Top + 18;

        NativeMethods.ForceTopmost(new WindowInteropHelper(this).Handle);

        await Task.Delay(_visibleDuration);
        if (IsVisible)
        {
            Close();
        }
    }

    private void ConfigureErrorLayout()
    {
        Width = Math.Min(820, SystemParameters.WorkArea.Width - 36);
        Height = Math.Min(320, SystemParameters.WorkArea.Height - 36);

        ArtworkColumn.Width = new GridLength(0);
        IconColumn.Width = new GridLength(42);
        ArtworkFrame.Visibility = Visibility.Collapsed;
        AlbumPlaceholder.Visibility = Visibility.Collapsed;
        AlbumArt.Visibility = Visibility.Collapsed;
        ArtistText.Visibility = Visibility.Collapsed;
        FooterText.Visibility = Visibility.Collapsed;

        TrackTitle.FontSize = 13;
        TrackTitle.FontWeight = FontWeights.SemiBold;
        TrackTitle.TextWrapping = TextWrapping.Wrap;
        TrackTitle.TextTrimming = TextTrimming.None;
        Grid.SetRowSpan(TrackTitle, 3);
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
