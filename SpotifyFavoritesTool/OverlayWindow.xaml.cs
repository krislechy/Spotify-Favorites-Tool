using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace SpotifyFavoritesTool;

public partial class OverlayWindow : Window
{
    public event EventHandler? FavoriteRequested;

    public OverlayWindow()
    {
        InitializeComponent();
        ShowMessage("Overlay готов", "Жду текущий трек");
    }

    public void ShowTrack(PlaybackTrack track)
    {
        TrackTitle.Text = track.Name;
        ArtistText.Text = track.Artists;
        SetFavoriteState(track.IsLiked);
        SetAlbumArt(track.AlbumImageUrl);
    }

    public void ShowMessage(string title, string message)
    {
        TrackTitle.Text = title;
        ArtistText.Text = message;
        FavoriteText.Text = "Избранное недоступно";
        FavoriteButton.Content = "♡";
        AlbumArt.Source = null;
        AlbumPlaceholder.Visibility = Visibility.Visible;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PlaceNearTopRight();
        NativeMethods.ForceTopmost(new WindowInteropHelper(this).Handle);
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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

    private void PlaceNearTopRight()
    {
        var workArea = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        Left = workArea.Right - Width - 18;
        Top = workArea.Top + 18;
    }
}
