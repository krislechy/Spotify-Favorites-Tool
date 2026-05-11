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
        var radius = GetRequiredRadius(position, width, height);
        var diameter = radius * 2;
        var scale = new ScaleTransform(0.08, 0.08);
        var ripple = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = GetRippleBrush(button),
            Opacity = 0,
            IsHitTestVisible = false,
            RenderTransform = scale,
            RenderTransformOrigin = new WpfPoint(0.5, 0.5)
        };

        Canvas.SetLeft(ripple, position.X - radius);
        Canvas.SetTop(ripple, position.Y - radius);
        host.Children.Add(ripple);

        var durationMs = Math.Clamp(GetDurationMilliseconds(button), 160, 1200);
        var opacity = Math.Clamp(GetPressedOpacity(button), 0.04, 0.5);
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var storyboard = new Storyboard();

        var opacityAnimation = new DoubleAnimationUsingKeyFrames();
        opacityAnimation.KeyFrames.Add(new DiscreteDoubleKeyFrame(opacity, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(opacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.22))));
        opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(duration)));
        Storyboard.SetTarget(opacityAnimation, ripple);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(UIElement.OpacityProperty));

        var scaleEase = new CubicEase { EasingMode = EasingMode.EaseOut };
        var scaleXAnimation = new DoubleAnimation(0.08, 1, new Duration(duration))
        {
            EasingFunction = scaleEase
        };
        Storyboard.SetTarget(scaleXAnimation, scale);
        Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath(ScaleTransform.ScaleXProperty));

        var scaleYAnimation = new DoubleAnimation(0.08, 1, new Duration(duration))
        {
            EasingFunction = scaleEase
        };
        Storyboard.SetTarget(scaleYAnimation, scale);
        Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath(ScaleTransform.ScaleYProperty));

        storyboard.Children.Add(opacityAnimation);
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);
        storyboard.Completed += (_, _) => host.Children.Remove(ripple);
        storyboard.Begin();
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
