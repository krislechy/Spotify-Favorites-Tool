using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.Brush;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfPoint = System.Windows.Point;

namespace SpotifyRelayOverlay;

public static class RippleEffectBehavior
{
    private static readonly MediaBrush DefaultRippleBrush = new SolidColorBrush(MediaColor.FromRgb(30, 215, 96));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(RippleEffectBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty RippleBrushProperty =
        DependencyProperty.RegisterAttached(
            "RippleBrush",
            typeof(MediaBrush),
            typeof(RippleEffectBehavior),
            new PropertyMetadata(DefaultRippleBrush));

    public static readonly DependencyProperty PressedOpacityProperty =
        DependencyProperty.RegisterAttached(
            "PressedOpacity",
            typeof(double),
            typeof(RippleEffectBehavior),
            new PropertyMetadata(0.24));

    public static readonly DependencyProperty DurationMillisecondsProperty =
        DependencyProperty.RegisterAttached(
            "DurationMilliseconds",
            typeof(int),
            typeof(RippleEffectBehavior),
            new PropertyMetadata(460));

    public static bool GetIsEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsEnabledProperty, value);
    }

    public static MediaBrush GetRippleBrush(DependencyObject obj)
    {
        return (MediaBrush)obj.GetValue(RippleBrushProperty);
    }

    public static void SetRippleBrush(DependencyObject obj, MediaBrush value)
    {
        obj.SetValue(RippleBrushProperty, value);
    }

    public static double GetPressedOpacity(DependencyObject obj)
    {
        return (double)obj.GetValue(PressedOpacityProperty);
    }

    public static void SetPressedOpacity(DependencyObject obj, double value)
    {
        obj.SetValue(PressedOpacityProperty, value);
    }

    public static int GetDurationMilliseconds(DependencyObject obj)
    {
        return (int)obj.GetValue(DurationMillisecondsProperty);
    }

    public static void SetDurationMilliseconds(DependencyObject obj, int value)
    {
        obj.SetValue(DurationMillisecondsProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not WpfButtonBase button)
        {
            return;
        }

        button.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        if ((bool)e.NewValue)
        {
            button.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not WpfButtonBase button || !button.IsEnabled)
        {
            return;
        }

        button.ApplyTemplate();
        if (button.Template.FindName("RippleHost", button) is not Canvas host)
        {
            return;
        }

        var width = host.ActualWidth > 0 ? host.ActualWidth : button.ActualWidth;
        var height = host.ActualHeight > 0 ? host.ActualHeight : button.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var position = e.GetPosition(host);
        var finalRadius = GetRequiredRadius(position, width, height) * 1.08;
        var initialRadius = Math.Min(10, finalRadius * 0.18);
        var initialDiameter = initialRadius * 2;
        var finalDiameter = finalRadius * 2;
        var ripple = new Ellipse
        {
            Width = initialDiameter,
            Height = initialDiameter,
            Fill = GetRippleBrush(button),
            Opacity = 0,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(ripple, position.X - initialRadius);
        Canvas.SetTop(ripple, position.Y - initialRadius);
        host.Children.Add(ripple);

        var durationMs = Math.Clamp(GetDurationMilliseconds(button), 160, 1200);
        var opacity = Math.Clamp(GetPressedOpacity(button), 0.04, 0.5);
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var expandEase = new CubicEase { EasingMode = EasingMode.EaseOut };

        ripple.BeginAnimation(FrameworkElement.WidthProperty, CreateExpansionAnimation(initialDiameter, finalDiameter, duration, expandEase));
        ripple.BeginAnimation(FrameworkElement.HeightProperty, CreateExpansionAnimation(initialDiameter, finalDiameter, duration, expandEase));
        ripple.BeginAnimation(Canvas.LeftProperty, CreateExpansionAnimation(position.X - initialRadius, position.X - finalRadius, duration, expandEase));
        ripple.BeginAnimation(Canvas.TopProperty, CreateExpansionAnimation(position.Y - initialRadius, position.Y - finalRadius, duration, expandEase));

        var opacityAnimation = CreateOpacityAnimation(opacity, durationMs);
        opacityAnimation.Completed += (_, _) => host.Children.Remove(ripple);
        ripple.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
    }

    private static DoubleAnimation CreateExpansionAnimation(double from, double to, TimeSpan duration, IEasingFunction easing)
    {
        return new DoubleAnimation(from, to, new Duration(duration))
        {
            EasingFunction = easing
        };
    }

    private static DoubleAnimationUsingKeyFrames CreateOpacityAnimation(double opacity, int durationMs)
    {
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var hold = TimeSpan.FromMilliseconds(durationMs * 0.58);
        var fadeIn = TimeSpan.FromMilliseconds(Math.Min(70, durationMs * 0.16));
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = new Duration(duration)
        };

        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(opacity, KeyTime.FromTimeSpan(fadeIn)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(opacity, KeyTime.FromTimeSpan(hold)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(duration)));
        return animation;
    }

    private static double GetRequiredRadius(WpfPoint origin, double width, double height)
    {
        var topLeft = GetDistance(origin, new WpfPoint(0, 0));
        var topRight = GetDistance(origin, new WpfPoint(width, 0));
        var bottomLeft = GetDistance(origin, new WpfPoint(0, height));
        var bottomRight = GetDistance(origin, new WpfPoint(width, height));
        return Math.Max(Math.Max(topLeft, topRight), Math.Max(bottomLeft, bottomRight));
    }

    private static double GetDistance(WpfPoint first, WpfPoint second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return Math.Sqrt((x * x) + (y * y));
    }
}
