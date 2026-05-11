using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InputMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPanel = System.Windows.Controls.Panel;
using WindowsPoint = System.Windows.Point;

namespace SpotifyRelayOverlay;

public sealed class CursorLightController : IDisposable
{
    private static readonly Duration FadeDuration = TimeSpan.FromMilliseconds(180);
    private static readonly IEasingFunction FadeEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
    private static readonly WpfColor GreenLightTarget = WpfColor.FromRgb(0x1E, 0xD7, 0x60);

    private readonly FrameworkElement _surface;
    private readonly Action<WpfBrush> _setBackground;
    private readonly RadialGradientBrush _brush;
    private readonly GradientStop _centerStop;
    private readonly GradientStop _middleStop;
    private readonly WpfColor _baseColor;
    private readonly WpfColor _centerColor;
    private readonly WpfColor _middleColor;

    private CursorLightController(FrameworkElement surface, Action<WpfBrush> setBackground, WpfColor baseColor)
    {
        _surface = surface;
        _setBackground = setBackground;
        _baseColor = baseColor;
        _centerColor = BrightenTowardGreen(baseColor, 0.15);
        _middleColor = BrightenTowardGreen(baseColor, 0.07);

        _centerStop = new GradientStop(_baseColor, 0);
        _middleStop = new GradientStop(_baseColor, 0.38);
        _brush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            Center = new WindowsPoint(0.5, 0.5),
            GradientOrigin = new WindowsPoint(0.5, 0.5),
            RadiusX = 0.62,
            RadiusY = 0.62
        };
        _brush.GradientStops.Add(_centerStop);
        _brush.GradientStops.Add(_middleStop);
        _brush.GradientStops.Add(new GradientStop(_baseColor, 1));
        _setBackground(_brush);

        _surface.MouseEnter += Surface_MouseEnter;
        _surface.MouseLeave += Surface_MouseLeave;
        _surface.MouseMove += Surface_MouseMove;
    }

    public static CursorLightController ForBorder(Border border, string baseColor)
    {
        return new CursorLightController(border, brush => border.Background = brush, ParseColor(baseColor));
    }

    public static CursorLightController ForPanel(WpfPanel panel, string baseColor)
    {
        return new CursorLightController(panel, brush => panel.Background = brush, ParseColor(baseColor));
    }

    public void Dispose()
    {
        _surface.MouseEnter -= Surface_MouseEnter;
        _surface.MouseLeave -= Surface_MouseLeave;
        _surface.MouseMove -= Surface_MouseMove;
        _centerStop.BeginAnimation(GradientStop.ColorProperty, null);
        _middleStop.BeginAnimation(GradientStop.ColorProperty, null);
    }

    private void Surface_MouseEnter(object sender, InputMouseEventArgs e)
    {
        MoveTo(e.GetPosition(_surface));
        AnimateColor(_centerStop, _centerColor);
        AnimateColor(_middleStop, _middleColor);
    }

    private void Surface_MouseLeave(object sender, InputMouseEventArgs e)
    {
        AnimateColor(_centerStop, _baseColor);
        AnimateColor(_middleStop, _baseColor);
    }

    private void Surface_MouseMove(object sender, InputMouseEventArgs e)
    {
        MoveTo(e.GetPosition(_surface));
    }

    private void MoveTo(WindowsPoint cursorPosition)
    {
        if (_surface.ActualWidth <= 0 || _surface.ActualHeight <= 0)
        {
            return;
        }

        var center = new WindowsPoint(
            Math.Clamp(cursorPosition.X / _surface.ActualWidth, 0, 1),
            Math.Clamp(cursorPosition.Y / _surface.ActualHeight, 0, 1));
        _brush.Center = center;
        _brush.GradientOrigin = center;
    }

    private void AnimateColor(GradientStop stop, WpfColor color)
    {
        var animation = new ColorAnimation(color, FadeDuration)
        {
            EasingFunction = FadeEase
        };
        stop.BeginAnimation(GradientStop.ColorProperty, animation);
    }

    private static WpfColor ParseColor(string value)
    {
        return (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(value)!;
    }

    private static WpfColor BrightenTowardGreen(WpfColor color, double amount)
    {
        return WpfColor.FromRgb(
            BlendChannel(color.R, GreenLightTarget.R, amount),
            BlendChannel(color.G, GreenLightTarget.G, amount),
            BlendChannel(color.B, GreenLightTarget.B, amount));
    }

    private static byte BlendChannel(byte current, byte target, double amount)
    {
        return (byte)Math.Round(current + (target - current) * amount);
    }
}
