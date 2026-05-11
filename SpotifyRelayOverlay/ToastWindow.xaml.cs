using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;

namespace SpotifyRelayOverlay;

public partial class ToastWindow : Window
{
    private readonly TimeSpan _visibleDuration = TimeSpan.FromSeconds(3.2);
    private FavoriteIconMode _iconMode = FavoriteIconMode.StaticUnliked;

    public ToastWindow(FavoriteToggleResult result)
    {
        InitializeComponent();

        ActionText.Text = result.Message;
        _iconMode = result.IsLiked ? FavoriteIconMode.Added : FavoriteIconMode.Removed;
        ConfigureTrackLayout(result.Track);
    }

    public ToastWindow(PlaybackTrack track)
    {
        InitializeComponent();

        var isLiked = track.IsLiked == true;
        ActionText.Text = isLiked ? "Сейчас играет · В Избранном" : "Сейчас играет · Не в Избранном";
        _iconMode = isLiked ? FavoriteIconMode.StaticLiked : FavoriteIconMode.StaticUnliked;
        ConfigureTrackLayout(track);
    }

    public ToastWindow(PlaybackTrack track, string actionText)
    {
        InitializeComponent();

        ActionText.Text = actionText;
        _iconMode = track.IsLiked == true ? FavoriteIconMode.StaticLiked : FavoriteIconMode.StaticUnliked;
        ConfigureTrackLayout(track);
    }

    public ToastWindow(string title, string message)
    {
        InitializeComponent();
        ConfigureErrorLayout();

        ActionText.Text = title;
        TrackTitle.Text = message;
        ArtistText.Text = string.Empty;
        SetErrorIcon();
        _visibleDuration = TimeSpan.FromSeconds(9);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + 18;
        Top = workArea.Top + 18;

        NativeMethods.ForceTopmost(new WindowInteropHelper(this).Handle);
        PlayFavoriteIconAnimation();

        await Task.Delay(_visibleDuration);
        if (IsVisible)
        {
            Close();
        }
    }

    private void ConfigureTrackLayout(PlaybackTrack track)
    {
        TrackTitle.Text = track.Name;
        ArtistText.Text = track.Artists;
        SetFavoriteIcon(track.IsLiked == true);
        SetAlbumArt(track.AlbumImageUrl);
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

    private void SetFavoriteIcon(bool isLiked)
    {
        ResetFavoriteIcon();

        if (_iconMode == FavoriteIconMode.Added)
        {
            return;
        }

        if (_iconMode == FavoriteIconMode.Removed)
        {
            HeartFill.Fill = CreateBrush(30, 215, 96);
            HeartFill.Opacity = 1;
            HeartScale.ScaleX = 1;
            HeartScale.ScaleY = 1;
            return;
        }

        if (isLiked)
        {
            HeartFill.Fill = CreateBrush(30, 215, 96);
            HeartFill.Opacity = 1;
            HeartScale.ScaleX = 1;
            HeartScale.ScaleY = 1;
            return;
        }

        HeartOutline.Opacity = 1;
    }

    private void SetErrorIcon()
    {
        ResetFavoriteIcon();
        FavoriteIconCanvas.Visibility = Visibility.Collapsed;
        ErrorIconText.Visibility = Visibility.Visible;
    }

    private void PlayFavoriteIconAnimation()
    {
        switch (_iconMode)
        {
            case FavoriteIconMode.Added:
                PlayAddedAnimation();
                break;
            case FavoriteIconMode.Removed:
                PlayRemovedAnimation();
                break;
        }
    }

    private void PlayAddedAnimation()
    {
        ResetFavoriteIcon();

        DropShape.Opacity = 0;
        DropScale.ScaleX = 0.25;
        DropScale.ScaleY = 0.25;
        DropTranslate.Y = -7;

        HeartFill.Fill = CreateBrush(30, 215, 96);
        HeartFill.Opacity = 0;
        HeartScale.ScaleX = 0.72;
        HeartScale.ScaleY = 0.72;
        HeartRotate.Angle = -8;

        Animate(DropShape, UIElement.OpacityProperty, 0, 0.95, 150);
        Animate(DropScale, ScaleTransform.ScaleXProperty, 0.25, 1.06, 280, easing: EaseOut());
        Animate(DropScale, ScaleTransform.ScaleYProperty, 0.25, 1.06, 280, easing: EaseOut());
        Animate(DropTranslate, TranslateTransform.YProperty, -7, 0, 260, easing: EaseOut());

        Animate(DropShape, UIElement.OpacityProperty, 0.95, 0, 180, 220);
        Animate(HeartFill, UIElement.OpacityProperty, 0, 1, 150, 230);
        AnimateKeyFrames(HeartScale, ScaleTransform.ScaleXProperty, (0.72, 230), (1.18, 430), (1, 620));
        AnimateKeyFrames(HeartScale, ScaleTransform.ScaleYProperty, (0.72, 230), (1.18, 430), (1, 620));
        Animate(HeartRotate, RotateTransform.AngleProperty, -8, 0, 360, 230, EaseOut());
    }

    private void PlayRemovedAnimation()
    {
        ResetFavoriteIcon();

        var dryingBrush = CreateBrush(30, 215, 96);
        HeartFill.Fill = dryingBrush;
        HeartFill.Opacity = 1;
        HeartScale.ScaleX = 1;
        HeartScale.ScaleY = 1;

        dryingBrush.BeginAnimation(
            SolidColorBrush.ColorProperty,
            new ColorAnimation(MediaColor.FromRgb(30, 215, 96), MediaColor.FromRgb(132, 154, 138), new Duration(TimeSpan.FromMilliseconds(430)))
            {
                BeginTime = TimeSpan.FromMilliseconds(60),
                EasingFunction = EaseOut()
            });

        Animate(HeartScale, ScaleTransform.ScaleXProperty, 1, 0.9, 420, 80, EaseInOut());
        Animate(HeartScale, ScaleTransform.ScaleYProperty, 1, 0.76, 420, 80, EaseInOut());
        Animate(CrackLines, UIElement.OpacityProperty, 0, 0.92, 120, 320);
        Animate(HeartFill, UIElement.OpacityProperty, 1, 0, 180, 560);
        Animate(CrackLines, UIElement.OpacityProperty, 0.92, 0, 160, 600);

        AnimateBrokenPiece(HeartPieceLeft, LeftPieceTranslate, LeftPieceRotate, -10, -3, -18, 500);
        AnimateBrokenPiece(HeartPieceRight, RightPieceTranslate, RightPieceRotate, 10, 1, 20, 540);
        AnimateBrokenPiece(HeartPieceCenter, CenterPieceTranslate, CenterPieceRotate, -1, 9, 8, 580);
    }

    private void AnimateBrokenPiece(UIElement piece, TranslateTransform translate, RotateTransform rotate, double x, double y, double angle, int delayMs)
    {
        piece.Opacity = 0;
        translate.X = 0;
        translate.Y = 0;
        rotate.Angle = 0;

        AnimateKeyFrames(piece, UIElement.OpacityProperty, (0, delayMs), (0.92, delayMs + 170), (0.56, delayMs + 560));
        Animate(translate, TranslateTransform.XProperty, 0, x, 520, delayMs, EaseOut());
        Animate(translate, TranslateTransform.YProperty, 0, y, 520, delayMs, EaseOut());
        Animate(rotate, RotateTransform.AngleProperty, 0, angle, 520, delayMs, EaseOut());
    }

    private void ResetFavoriteIcon()
    {
        ErrorIconText.Visibility = Visibility.Collapsed;
        FavoriteIconCanvas.Visibility = Visibility.Visible;

        DropShape.BeginAnimation(UIElement.OpacityProperty, null);
        HeartFill.BeginAnimation(UIElement.OpacityProperty, null);
        HeartOutline.BeginAnimation(UIElement.OpacityProperty, null);
        CrackLines.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceLeft.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceRight.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceCenter.BeginAnimation(UIElement.OpacityProperty, null);

        DropShape.Opacity = 0;
        HeartFill.Opacity = 0;
        HeartOutline.Opacity = 0;
        CrackLines.Opacity = 0;
        HeartPieceLeft.Opacity = 0;
        HeartPieceRight.Opacity = 0;
        HeartPieceCenter.Opacity = 0;

        DropScale.ScaleX = 0.25;
        DropScale.ScaleY = 0.25;
        DropTranslate.Y = -5;
        HeartScale.ScaleX = 1;
        HeartScale.ScaleY = 1;
        HeartRotate.Angle = 0;
        HeartTranslate.X = 0;
        HeartTranslate.Y = 0;
        LeftPieceTranslate.X = 0;
        LeftPieceTranslate.Y = 0;
        RightPieceTranslate.X = 0;
        RightPieceTranslate.Y = 0;
        CenterPieceTranslate.X = 0;
        CenterPieceTranslate.Y = 0;
        LeftPieceRotate.Angle = 0;
        RightPieceRotate.Angle = 0;
        CenterPieceRotate.Angle = 0;
    }

    private static void Animate(UIElement target, DependencyProperty property, double from, double to, int durationMs, int delayMs = 0, IEasingFunction? easing = null)
    {
        target.BeginAnimation(property, CreateDoubleAnimation(from, to, durationMs, delayMs, easing));
    }

    private static void Animate(Animatable target, DependencyProperty property, double from, double to, int durationMs, int delayMs = 0, IEasingFunction? easing = null)
    {
        target.BeginAnimation(property, CreateDoubleAnimation(from, to, durationMs, delayMs, easing));
    }

    private static DoubleAnimation CreateDoubleAnimation(double from, double to, int durationMs, int delayMs, IEasingFunction? easing)
    {
        return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = easing
        };
    }

    private static void AnimateKeyFrames(UIElement target, DependencyProperty property, params (double Value, int AtMs)[] frames)
    {
        target.BeginAnimation(property, CreateKeyFrameAnimation(frames));
    }

    private static void AnimateKeyFrames(Animatable target, DependencyProperty property, params (double Value, int AtMs)[] frames)
    {
        target.BeginAnimation(property, CreateKeyFrameAnimation(frames));
    }

    private static DoubleAnimationUsingKeyFrames CreateKeyFrameAnimation(params (double Value, int AtMs)[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames();
        foreach (var frame in frames)
        {
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(frame.Value, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(frame.AtMs)))
            {
                EasingFunction = EaseOut()
            });
        }

        return animation;
    }

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(MediaColor.FromRgb(red, green, blue));
    }

    private static IEasingFunction EaseOut()
    {
        return new CubicEase { EasingMode = EasingMode.EaseOut };
    }

    private static IEasingFunction EaseInOut()
    {
        return new CubicEase { EasingMode = EasingMode.EaseInOut };
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

    private enum FavoriteIconMode
    {
        StaticLiked,
        StaticUnliked,
        Added,
        Removed
    }
}
