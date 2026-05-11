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
        GlowScale.ScaleX = 0.36;
        GlowScale.ScaleY = 0.36;

        Animate(HeartFill, UIElement.OpacityProperty, 0, 1, 120);
        AnimateKeyFrames(HeartScale, ScaleTransform.ScaleXProperty, (0.26, 0), (1.34, 170), (0.86, 330), (1.2, 500), (0.96, 650), (1.05, 800), (1, 980));
        AnimateKeyFrames(HeartScale, ScaleTransform.ScaleYProperty, (0.26, 0), (1.34, 170), (0.86, 330), (1.2, 500), (0.96, 650), (1.05, 800), (1, 980));
        Animate(HeartRotate, RotateTransform.AngleProperty, -4, 0, 280, easing: EaseOut());

        AnimateKeyFrames(HeartGlow, UIElement.OpacityProperty, (0, 0), (1, 110), (0.76, 360), (0.48, 760), (0, 1450));
        AnimateKeyFrames(GlowScale, ScaleTransform.ScaleXProperty, (0.36, 0), (1.24, 240), (0.95, 560), (1.42, 1450));
        AnimateKeyFrames(GlowScale, ScaleTransform.ScaleYProperty, (0.36, 0), (1.24, 240), (0.95, 560), (1.42, 1450));

        AnimateSparkle(SparkleTop, SparkleTopScale, 90, 1.05);
        AnimateSparkle(SparkleLeft, SparkleLeftScale, 210, 0.9);
        AnimateSparkle(SparkleRight, SparkleRightScale, 310, 0.95);
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
        BulletHole.Opacity = 0;
        BulletRing.Opacity = 0;
        BulletHoleScale.ScaleX = 0.2;
        BulletHoleScale.ScaleY = 0.2;
        BulletRingScale.ScaleX = 0.35;
        BulletRingScale.ScaleY = 0.35;
        CrackScale.ScaleX = 0.15;
        CrackScale.ScaleY = 0.15;

        dryingBrush.BeginAnimation(
            SolidColorBrush.ColorProperty,
            new ColorAnimation(MediaColor.FromRgb(30, 215, 96), MediaColor.FromRgb(25, 31, 27), new Duration(TimeSpan.FromMilliseconds(520)))
            {
                BeginTime = TimeSpan.FromMilliseconds(60),
                EasingFunction = EaseOut()
            });

        AnimateKeyFrames(HeartScale, ScaleTransform.ScaleXProperty, (1, 0), (1.04, 120), (0.96, 260), (1, 420));
        AnimateKeyFrames(HeartScale, ScaleTransform.ScaleYProperty, (1, 0), (1.04, 120), (0.96, 260), (1, 420));
        Animate(BulletHole, UIElement.OpacityProperty, 0, 1, 80, 130);
        Animate(BulletHoleScale, ScaleTransform.ScaleXProperty, 0.2, 1, 130, 130, EaseOut());
        Animate(BulletHoleScale, ScaleTransform.ScaleYProperty, 0.2, 1, 130, 130, EaseOut());
        AnimateKeyFrames(BulletRing, UIElement.OpacityProperty, (0, 120), (0.9, 190), (0.28, 430), (0, 780));
        AnimateKeyFrames(BulletRingScale, ScaleTransform.ScaleXProperty, (0.35, 120), (1.1, 260), (1.65, 780));
        AnimateKeyFrames(BulletRingScale, ScaleTransform.ScaleYProperty, (0.35, 120), (1.1, 260), (1.65, 780));
        AnimateKeyFrames(CrackLines, UIElement.OpacityProperty, (0, 230), (1, 360), (0.82, 920), (0, 1280));
        AnimateKeyFrames(CrackScale, ScaleTransform.ScaleXProperty, (0.15, 230), (1, 470), (1.08, 1280));
        AnimateKeyFrames(CrackScale, ScaleTransform.ScaleYProperty, (0.15, 230), (1, 470), (1.08, 1280));
        Animate(HeartFill, UIElement.OpacityProperty, 1, 0, 120, 650);
        Animate(BulletHole, UIElement.OpacityProperty, 1, 0, 180, 760);

        AnimateBrokenPiece(HeartPieceLeft, LeftPieceTranslate, LeftPieceRotate, -12, -6, -18, 620);
        AnimateBrokenPiece(HeartPieceRight, RightPieceTranslate, RightPieceRotate, 12, -4, 18, 640);
        AnimateBrokenPiece(HeartPieceCenter, CenterPieceTranslate, CenterPieceRotate, 1, 13, 7, 660);
        AnimateBrokenPiece(HeartPieceTop, TopPieceTranslate, TopPieceRotate, -1, -13, -7, 610);
        AnimateBrokenPiece(HeartPieceLowerLeft, LowerLeftPieceTranslate, LowerLeftPieceRotate, -14, 11, -24, 700);
        AnimateBrokenPiece(HeartPieceLowerRight, LowerRightPieceTranslate, LowerRightPieceRotate, 14, 11, 24, 720);
    }

    private void AnimateAsh(UIElement ash, TranslateTransform translate, double x, double y, int delayMs)
    {
        ash.Opacity = 0;
        translate.X = 0;
        translate.Y = 0;

        AnimateKeyFrames(ash, UIElement.OpacityProperty, (0, delayMs), (0.82, delayMs + 120), (0, delayMs + 780));
        Animate(translate, TranslateTransform.XProperty, 0, x, 760, delayMs, EaseOut());
        Animate(translate, TranslateTransform.YProperty, 0, y, 760, delayMs, EaseOut());
    }

    private void AnimateDust(UIElement dust, TranslateTransform translate, double x, double y, int delayMs)
    {
        dust.Opacity = 0;
        translate.X = 0;
        translate.Y = 0;

        AnimateKeyFrames(dust, UIElement.OpacityProperty, (0, delayMs), (0.76, delayMs + 100), (0.42, delayMs + 420), (0, delayMs + 900));
        Animate(translate, TranslateTransform.XProperty, 0, x, 900, delayMs, EaseOut());
        Animate(translate, TranslateTransform.YProperty, 0, y, 900, delayMs, EaseOut());
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
        DarkMist.BeginAnimation(UIElement.OpacityProperty, null);
        HeartGlow.BeginAnimation(UIElement.OpacityProperty, null);
        SparkleTop.BeginAnimation(UIElement.OpacityProperty, null);
        SparkleLeft.BeginAnimation(UIElement.OpacityProperty, null);
        SparkleRight.BeginAnimation(UIElement.OpacityProperty, null);
        DustOne.BeginAnimation(UIElement.OpacityProperty, null);
        DustTwo.BeginAnimation(UIElement.OpacityProperty, null);
        DustThree.BeginAnimation(UIElement.OpacityProperty, null);
        DustFour.BeginAnimation(UIElement.OpacityProperty, null);
        DustFive.BeginAnimation(UIElement.OpacityProperty, null);
        DustSix.BeginAnimation(UIElement.OpacityProperty, null);
        AshOne.BeginAnimation(UIElement.OpacityProperty, null);
        AshTwo.BeginAnimation(UIElement.OpacityProperty, null);
        AshThree.BeginAnimation(UIElement.OpacityProperty, null);
        HeartFill.BeginAnimation(UIElement.OpacityProperty, null);
        HeartOutline.BeginAnimation(UIElement.OpacityProperty, null);
        CrackLines.BeginAnimation(UIElement.OpacityProperty, null);
        BulletHole.BeginAnimation(UIElement.OpacityProperty, null);
        BulletRing.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceLeft.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceRight.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceCenter.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceTop.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceLowerLeft.BeginAnimation(UIElement.OpacityProperty, null);
        HeartPieceLowerRight.BeginAnimation(UIElement.OpacityProperty, null);

        DropShape.Opacity = 0;
        DarkMist.Opacity = 0;
        HeartGlow.Opacity = 0;
        SparkleTop.Opacity = 0;
        SparkleLeft.Opacity = 0;
        SparkleRight.Opacity = 0;
        DustOne.Opacity = 0;
        DustTwo.Opacity = 0;
        DustThree.Opacity = 0;
        DustFour.Opacity = 0;
        DustFive.Opacity = 0;
        DustSix.Opacity = 0;
        AshOne.Opacity = 0;
        AshTwo.Opacity = 0;
        AshThree.Opacity = 0;
        HeartFill.Opacity = 0;
        HeartOutline.Opacity = 0;
        CrackLines.Opacity = 0;
        BulletHole.Opacity = 0;
        BulletRing.Opacity = 0;
        HeartPieceLeft.Opacity = 0;
        HeartPieceRight.Opacity = 0;
        HeartPieceCenter.Opacity = 0;
        HeartPieceTop.Opacity = 0;
        HeartPieceLowerLeft.Opacity = 0;
        HeartPieceLowerRight.Opacity = 0;

        DropScale.ScaleX = 0.25;
        DropScale.ScaleY = 0.25;
        DropTranslate.Y = -5;
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
        SparkleTopRotate.Angle = 0;
        CrackScale.ScaleX = 0.15;
        CrackScale.ScaleY = 0.15;
        BulletHoleScale.ScaleX = 0.2;
        BulletHoleScale.ScaleY = 0.2;
        BulletRingScale.ScaleX = 0.35;
        BulletRingScale.ScaleY = 0.35;
        DustOneTranslate.X = 0;
        DustOneTranslate.Y = 0;
        DustTwoTranslate.X = 0;
        DustTwoTranslate.Y = 0;
        DustThreeTranslate.X = 0;
        DustThreeTranslate.Y = 0;
        DustFourTranslate.X = 0;
        DustFourTranslate.Y = 0;
        DustFiveTranslate.X = 0;
        DustFiveTranslate.Y = 0;
        DustSixTranslate.X = 0;
        DustSixTranslate.Y = 0;
        AshOneTranslate.X = 0;
        AshOneTranslate.Y = 0;
        AshTwoTranslate.X = 0;
        AshTwoTranslate.Y = 0;
        AshThreeTranslate.X = 0;
        AshThreeTranslate.Y = 0;
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
