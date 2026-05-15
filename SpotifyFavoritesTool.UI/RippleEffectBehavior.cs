using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.Brush;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPanel = System.Windows.Controls.Panel;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace SpotifyFavoritesTool.UI;

public static class RippleEffectBehavior
{
    private const string RippleHostName = "RippleHost";

    private static readonly MediaBrush DefaultRippleBrush = new SolidColorBrush(MediaColor.FromRgb(30, 215, 96));

    private static readonly DependencyProperty ActiveFillProperty =
        DependencyProperty.RegisterAttached(
            "ActiveFill",
            typeof(FrameworkElement),
            typeof(RippleEffectBehavior),
            new PropertyMetadata(null));

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
        button.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
        button.LostMouseCapture -= OnLostMouseCapture;
        button.MouseLeave -= OnMouseLeave;
        button.Unloaded -= OnUnloaded;

        if ((bool)e.NewValue)
        {
            button.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            button.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            button.LostMouseCapture += OnLostMouseCapture;
            button.MouseLeave += OnMouseLeave;
            button.Unloaded += OnUnloaded;
        }
        else
        {
            FinishActiveFill(button, removeImmediately: true);
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not WpfButtonBase button || !button.IsEnabled)
        {
            return;
        }

        button.ApplyTemplate();
        if (button.Template.FindName(RippleHostName, button) is not Canvas host)
        {
            return;
        }

        var width = host.ActualWidth > 0 ? host.ActualWidth : button.ActualWidth;
        var height = host.ActualHeight > 0 ? host.ActualHeight : button.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        ApplyRoundedClip(button, host, width, height);
        FinishActiveFill(button, removeImmediately: true);

        var position = e.GetPosition(host);
        var originX = Math.Clamp(position.X / width, 0, 1);
        var fill = new Border
        {
            Width = width,
            Height = height,
            Background = GetRippleBrush(button),
            CornerRadius = GetRootCornerRadius(button),
            Opacity = 0,
            IsHitTestVisible = false,
            RenderTransformOrigin = new WpfPoint(originX, 0.5),
            RenderTransform = new ScaleTransform(0, 1)
        };

        Canvas.SetLeft(fill, 0);
        Canvas.SetTop(fill, 0);
        host.Children.Add(fill);
        SetActiveFill(button, fill);

        var durationMs = Math.Clamp(GetDurationMilliseconds(button), 160, 1200);
        var opacity = Math.Clamp(GetPressedOpacity(button), 0.04, 0.5);
        var scale = (ScaleTransform)fill.RenderTransform;

        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateHoldAnimation(0, 1, durationMs, EaseOut()));

        fill.BeginAnimation(
            UIElement.OpacityProperty,
            CreateHoldAnimation(0, opacity, Math.Min(90, durationMs / 3), EaseOut()));
    }

    private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is WpfButtonBase button)
        {
            FinishActiveFill(button, removeImmediately: false);
        }
    }

    private static void OnLostMouseCapture(object sender, WpfMouseEventArgs e)
    {
        if (sender is WpfButtonBase button && Mouse.LeftButton != MouseButtonState.Pressed)
        {
            FinishActiveFill(button, removeImmediately: false);
        }
    }

    private static void OnMouseLeave(object sender, WpfMouseEventArgs e)
    {
        if (sender is WpfButtonBase button && Mouse.LeftButton != MouseButtonState.Pressed)
        {
            FinishActiveFill(button, removeImmediately: false);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButtonBase button)
        {
            FinishActiveFill(button, removeImmediately: true);
        }
    }

    private static void FinishActiveFill(WpfButtonBase button, bool removeImmediately)
    {
        if (GetActiveFill(button) is not FrameworkElement fill)
        {
            return;
        }

        SetActiveFill(button, null);

        if (removeImmediately)
        {
            RemoveFromParent(fill);
            return;
        }

        var fadeOut = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(170),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        fadeOut.Completed += (_, _) => RemoveFromParent(fill);
        fill.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private static FrameworkElement? GetActiveFill(DependencyObject obj)
    {
        return (FrameworkElement?)obj.GetValue(ActiveFillProperty);
    }

    private static void SetActiveFill(DependencyObject obj, FrameworkElement? value)
    {
        obj.SetValue(ActiveFillProperty, value);
    }

    private static void RemoveFromParent(FrameworkElement element)
    {
        if (element.Parent is WpfPanel panel)
        {
            panel.Children.Remove(element);
        }
    }

    private static void ApplyRoundedClip(WpfButtonBase button, Canvas host, double width, double height)
    {
        var radius = GetRootCornerRadius(button).TopLeft;
        radius = Math.Clamp(radius, 0, Math.Min(width, height) / 2);
        host.Clip = new RectangleGeometry(new WpfRect(0, 0, width, height), radius, radius);
    }

    private static CornerRadius GetRootCornerRadius(WpfButtonBase button)
    {
        return button.Template.FindName("Root", button) is Border root
            ? root.CornerRadius
            : new CornerRadius();
    }

    private static DoubleAnimation CreateHoldAnimation(double from, double to, int durationMs, IEasingFunction easing)
    {
        return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
    }

    private static IEasingFunction EaseOut()
    {
        return new CubicEase { EasingMode = EasingMode.EaseOut };
    }
}
