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
    private const double LightRadius = 150;

    private static readonly Duration FadeDuration = TimeSpan.FromMilliseconds(180);
    private static readonly IEasingFunction FadeEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
    private static readonly WpfColor GreenLightTarget = WpfColor.FromRgb(0x1E, 0xD7, 0x60);

    private readonly FrameworkElement _surface;
    private readonly Action<WpfBrush> _setBackground;
    private readonly RadialGradientBrush _brush;
    private readonly GradientStop _coreStop;
    private readonly GradientStop _sheenStop;
    private readonly GradientStop _diffuseStop;
    private readonly GradientStop _tailStop;
    private readonly WpfColor _baseColor;
    private readonly WpfColor _coreColor;
    private readonly WpfColor _sheenColor;
    private readonly WpfColor _diffuseColor;
    private readonly WpfColor _tailColor;

    private CursorLightController(FrameworkElement surface, Action<WpfBrush> setBackground, WpfColor baseColor)
    {
        _surface = surface;
        _setBackground = setBackground;
        _baseColor = baseColor;
        _coreColor = BrightenTowardGreen(baseColor, 0.30);
        _sheenColor = BrightenTowardGreen(baseColor, 0.19);
        _diffuseColor = BrightenTowardGreen(baseColor, 0.085);
        _tailColor = BrightenTowardGreen(baseColor, 0.035);

        _coreStop = new GradientStop(_baseColor, 0);
        _sheenStop = new GradientStop(_baseColor, 0.24);
        _diffuseStop = new GradientStop(_baseColor, 0.72);
        _tailStop = new GradientStop(_baseColor, 0.96);
        _brush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new WindowsPoint(0, 0),
            GradientOrigin = new WindowsPoint(0, 0),
            RadiusX = LightRadius,
            RadiusY = LightRadius
        };
        _brush.GradientStops.Add(_coreStop);
        _brush.GradientStops.Add(_sheenStop);
        _brush.GradientStops.Add(_diffuseStop);
        _brush.GradientStops.Add(_tailStop);
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
        ClearAnimation(_coreStop);
        ClearAnimation(_sheenStop);
        ClearAnimation(_diffuseStop);
        ClearAnimation(_tailStop);
    }

    private void Surface_MouseEnter(object sender, InputMouseEventArgs e)
    {
        MoveTo(e.GetPosition(_surface));
        AnimateMaterial(_coreColor, _sheenColor, _diffuseColor, _tailColor);
    }

    private void Surface_MouseLeave(object sender, InputMouseEventArgs e)
    {
        AnimateMaterial(_baseColor, _baseColor, _baseColor, _baseColor);
    }

    private void Surface_MouseMove(object sender, InputMouseEventArgs e)
    {
        MoveTo(e.GetPosition(_surface));
    }

    private void MoveTo(WindowsPoint cursorPosition)
    {
        var center = new WindowsPoint(
            Math.Clamp(cursorPosition.X, 0, _surface.ActualWidth),
            Math.Clamp(cursorPosition.Y, 0, _surface.ActualHeight));
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

    private void AnimateMaterial(WpfColor core, WpfColor sheen, WpfColor diffuse, WpfColor tail)
    {
        AnimateColor(_coreStop, core);
        AnimateColor(_sheenStop, sheen);
        AnimateColor(_diffuseStop, diffuse);
        AnimateColor(_tailStop, tail);
    }

    private static void ClearAnimation(GradientStop stop)
    {
        stop.BeginAnimation(GradientStop.ColorProperty, null);
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
