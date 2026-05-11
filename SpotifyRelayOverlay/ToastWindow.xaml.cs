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

        HeartFill.Fill = CreateBrush(30, 215, 96);
        HeartFill.Opacity = 0;
        HeartScale.ScaleX = 0.26;
        HeartScale.ScaleY = 0.26;
        HeartRotate.Angle = -4;

        HeartGlow.Opacity = 0;
        GlowScale.ScaleX = 0.45;
        GlowScale.ScaleY = 0.45;
        DivineRays.Opacity = 0;
        DivineRaysScale.ScaleX = 0.65;
        DivineRaysScale.ScaleY = 0.65;
        HaloRing.Opacity = 0;
        HaloRingScale.ScaleX = 0.55;
        HaloRingScale.ScaleY = 0.55;

        Animate(HeartFill, UIElement.OpacityProperty, 0, 1, 120);
        AnimateKeyFrames(HeartScale, ScaleTransform.ScaleXProperty, (0.26, 0), (1.24, 190), (0.92, 350), (1.13, 520), (1, 720));
        AnimateKeyFrames(HeartScale, ScaleTransform.ScaleYProperty, (0.26, 0), (1.24, 190), (0.92, 350), (1.13, 520), (1, 720));
        Animate(HeartRotate, RotateTransform.AngleProperty, -4, 0, 280, easing: EaseOut());

        AnimateKeyFrames(HeartGlow, UIElement.OpacityProperty, (0, 0), (1, 110), (0.62, 430), (0.18, 980), (0, 1400));
        AnimateKeyFrames(GlowScale, ScaleTransform.ScaleXProperty, (0.45, 0), (1.34, 240), (1.08, 660), (1.55, 1400));
        AnimateKeyFrames(GlowScale, ScaleTransform.ScaleYProperty, (0.45, 0), (1.34, 240), (1.08, 660), (1.55, 1400));

        AnimateKeyFrames(HaloRing, UIElement.OpacityProperty, (0, 0), (0.92, 120), (0.35, 520), (0, 1250));
        AnimateKeyFrames(HaloRingScale, ScaleTransform.ScaleXProperty, (0.55, 0), (1.03, 260), (1.22, 850), (1.48, 1250));
        AnimateKeyFrames(HaloRingScale, ScaleTransform.ScaleYProperty, (0.55, 0), (1.03, 260), (1.22, 850), (1.48, 1250));

        AnimateKeyFrames(DivineRays, UIElement.OpacityProperty, (0, 40), (0.95, 170), (0.38, 540), (0, 1050));
        AnimateKeyFrames(DivineRaysScale, ScaleTransform.ScaleXProperty, (0.65, 40), (1.1, 230), (1.28, 1050));
        AnimateKeyFrames(DivineRaysScale, ScaleTransform.ScaleYProperty, (0.65, 40), (1.1, 230), (1.28, 1050));

        AnimateSparkle(SparkleTop, SparkleTopScale, 90, 1.05);
        AnimateSparkle(SparkleLeft, SparkleLeftScale, 210, 0.9);
        AnimateSparkle(SparkleRight, SparkleRightScale, 310, 0.95);
        AnimateSparkle(SparkleBottom, SparkleBottomScale, 420, 0.82);
        Animate(SparkleTopRotate, RotateTransform.AngleProperty, -18, 24, 620, 90, EaseOut());
    }

    private void AnimateSparkle(UIElement sparkle, ScaleTransform scale, int delayMs, double maxScale)
    {
        AnimateKeyFrames(sparkle, UIElement.OpacityProperty, (0, delayMs), (0.95, delayMs + 100), (0, delayMs + 520));
        AnimateKeyFrames(scale, ScaleTransform.ScaleXProperty, (0.25, delayMs), (maxScale, delayMs + 150), (0.35, delayMs + 520));
        AnimateKeyFrames(scale, ScaleTransform.ScaleYProperty, (0.25, delayMs), (maxScale, delayMs + 150), (0.35, delayMs + 520));
    }

    private void PlayRemovedAnimation()
    {
        ResetFavoriteIcon();

        var dryingBrush = CreateBrush(30, 215, 96);
        HeartFill.Fill = dryingBrush;
        HeartFill.Opacity = 1;
        HeartScale.ScaleX = 1;
        HeartScale.ScaleY = 1;
        DarkMist.Opacity = 0;
        DarkMistScale.ScaleX = 0.62;
        DarkMistScale.ScaleY = 0.62;

        dryingBrush.BeginAnimation(
            SolidColorBrush.ColorProperty,
            new ColorAnimation(MediaColor.FromRgb(30, 215, 96), MediaColor.FromRgb(54, 66, 58), new Duration(TimeSpan.FromMilliseconds(520)))
            {
                BeginTime = TimeSpan.FromMilliseconds(60),
                EasingFunction = EaseOut()
            });

        AnimateKeyFrames(DarkMist, UIElement.OpacityProperty, (0, 0), (0.78, 180), (0.46, 700), (0, 1400));
        AnimateKeyFrames(DarkMistScale, ScaleTransform.ScaleXProperty, (0.62, 0), (1.05, 360), (1.4, 1400));
        AnimateKeyFrames(DarkMistScale, ScaleTransform.ScaleYProperty, (0.62, 0), (0.9, 360), (1.28, 1400));

        Animate(HeartScale, ScaleTransform.ScaleXProperty, 1, 0.84, 480, 80, EaseInOut());
        Animate(HeartScale, ScaleTransform.ScaleYProperty, 1, 0.66, 480, 80, EaseInOut());
        Animate(CrackLines, UIElement.OpacityProperty, 0, 1, 130, 300);
        Animate(HeartFill, UIElement.OpacityProperty, 1, 0, 180, 600);
        Animate(CrackLines, UIElement.OpacityProperty, 1, 0, 220, 720);

        AnimateBrokenPiece(HeartPieceLeft, LeftPieceTranslate, LeftPieceRotate, -10, -3, -18, 500);
        AnimateBrokenPiece(HeartPieceRight, RightPieceTranslate, RightPieceRotate, 10, 1, 20, 540);
        AnimateBrokenPiece(HeartPieceCenter, CenterPieceTranslate, CenterPieceRotate, -1, 9, 8, 580);
        AnimateBrokenPiece(HeartPieceTop, TopPieceTranslate, TopPieceRotate, 0, -11, -8, 500);
        AnimateBrokenPiece(HeartPieceLowerLeft, LowerLeftPieceTranslate, LowerLeftPieceRotate, -13, 10, -28, 620);
        AnimateBrokenPiece(HeartPieceLowerRight, LowerRightPieceTranslate, LowerRightPieceRotate, 13, 9, 30, 650);

        AnimateAsh(AshOne, AshOneTranslate, -13, -10, 380);
        AnimateAsh(AshTwo, AshTwoTranslate, 10, -12, 460);
        AnimateAsh(AshThree, AshThreeTranslate, 2, 15, 560);
    }

    private void AnimateAsh(UIElement ash, TranslateTransform translate, double x, double y, int delayMs)
    {
        ash.Opacity = 0;
        translate.X = 0;
        translate.Y = 0;

        AnimateKeyFrames(ash, UIElement.OpacityProperty, (0, delayMs), (0.8, delayMs + 120), (0, delayMs + 780));
        Animate(translate, TranslateTransform.XProperty, 0, x, 760, delayMs, EaseOut());
        Animate(translate, TranslateTransform.YProperty, 0, y, 760, delayMs, EaseOut());
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
        DivineRays.BeginAnimation(UIElement.OpacityProperty, null);
        HaloRing.BeginAnimation(UIElement.OpacityProperty, null);
        DarkMist.BeginAnimation(UIElement.OpacityProperty, null);
        HeartGlow.BeginAnimation(UIElement.OpacityProperty, null);
        SparkleTop.BeginAnimation(UIElement.OpacityProperty, null);
        SparkleLeft.BeginAnimation(UIElement.OpacityProperty, null);
        SparkleRight.BeginAnimation(UIElement.OpacityProperty, null);
        SparkleBottom.BeginAnimation(UIElement.OpacityProperty, null);
        AshOne.BeginAnimation(UIElement.OpacityProperty, null);
        AshTwo.BeginAnimation(UIElement.OpacityProperty, null);
        AshThree.BeginAnimation(UIElement.OpacityProperty, null);
        HeartFill.BeginAnimation(UIElement.OpacityProperty, null);
        HeartOutline.BeginAnimation(UIElement.OpacityProperty, null);
        CrackLines.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceLeft.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceRight.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceCenter.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceTop.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceLowerLeft.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceLowerRight.BeginAnimation(UIElement.OpacityProperty, null);

        DropShape.Opacity = 0;
        DivineRays.Opacity = 0;
        HaloRing.Opacity = 0;
        DarkMist.Opacity = 0;
        HeartGlow.Opacity = 0;
        SparkleTop.Opacity = 0;
        SparkleLeft.Opacity = 0;
        SparkleRight.Opacity = 0;
        SparkleBottom.Opacity = 0;
        AshOne.Opacity = 0;
        AshTwo.Opacity = 0;
        AshThree.Opacity = 0;
        HeartFill.Opacity = 0;
        HeartOutline.Opacity = 0;
        CrackLines.Opacity = 0;
        HeartPieceLeft.Opacity = 0;
        HeartPieceRight.Opacity = 0;
        HeartPieceCenter.Opacity = 0;
        HeartPieceTop.Opacity = 0;
        HeartPieceLowerLeft.Opacity = 0;
        HeartPieceLowerRight.Opacity = 0;

        DropScale.ScaleX = 0.25;
        DropScale.ScaleY = 0.25;
        DropTranslate.Y = -5;
        DivineRaysScale.ScaleX = 0.65;
        DivineRaysScale.ScaleY = 0.65;
        HaloRingScale.ScaleX = 0.55;
        HaloRingScale.ScaleY = 0.55;
        DarkMistScale.ScaleX = 0.62;
        DarkMistScale.ScaleY = 0.62;
        GlowScale.ScaleX = 0.55;
        GlowScale.ScaleY = 0.55;
        SparkleTopScale.ScaleX = 0.25;
        SparkleTopScale.ScaleY = 0.25;
        SparkleLeftScale.ScaleX = 0.25;
        SparkleLeftScale.ScaleY = 0.25;
        SparkleRightScale.ScaleX = 0.25;
        SparkleRightScale.ScaleY = 0.25;
        SparkleBottomScale.ScaleX = 0.25;
        SparkleBottomScale.ScaleY = 0.25;
        SparkleTopRotate.Angle = 0;
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
        TopPieceTranslate.X = 0;
        TopPieceTranslate.Y = 0;
        LowerLeftPieceTranslate.X = 0;
        LowerLeftPieceTranslate.Y = 0;
        LowerRightPieceTranslate.X = 0;
        LowerRightPieceTranslate.Y = 0;
        AshOneTranslate.X = 0;
        AshOneTranslate.Y = 0;
        AshTwoTranslate.X = 0;
        AshTwoTranslate.Y = 0;
        AshThreeTranslate.X = 0;
        AshThreeTranslate.Y = 0;
        LeftPieceRotate.Angle = 0;
        RightPieceRotate.Angle = 0;
        CenterPieceRotate.Angle = 0;
        TopPieceRotate.Angle = 0;
        LowerLeftPieceRotate.Angle = 0;
        LowerRightPieceRotate.Angle = 0;
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
