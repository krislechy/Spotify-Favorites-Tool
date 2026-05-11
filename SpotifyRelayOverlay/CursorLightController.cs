using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InputMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WindowsPoint = System.Windows.Point;

namespace SpotifyRelayOverlay;

public sealed class CursorLightController : IDisposable
{
    private static readonly Duration FadeDuration = TimeSpan.FromMilliseconds(180);
    private static readonly IEasingFunction FadeEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };

    private readonly FrameworkElement _surface;
    private readonly UIElement _light;
    private readonly TranslateTransform _transform;
    private readonly double _lightSize;
    private readonly double _visibleOpacity;

    public CursorLightController(
        FrameworkElement surface,
        UIElement light,
        TranslateTransform transform,
        double lightSize,
        double visibleOpacity)
    {
        _surface = surface;
        _light = light;
        _transform = transform;
        _lightSize = lightSize;
        _visibleOpacity = visibleOpacity;

        _surface.MouseEnter += Surface_MouseEnter;
        _surface.MouseLeave += Surface_MouseLeave;
        _surface.MouseMove += Surface_MouseMove;
    }

    public void Dispose()
    {
        _surface.MouseEnter -= Surface_MouseEnter;
        _surface.MouseLeave -= Surface_MouseLeave;
        _surface.MouseMove -= Surface_MouseMove;
        _light.BeginAnimation(UIElement.OpacityProperty, null);
    }

    private void Surface_MouseEnter(object sender, InputMouseEventArgs e)
    {
        MoveTo(e.GetPosition(_surface));
        AnimateOpacity(_visibleOpacity);
    }

    private void Surface_MouseLeave(object sender, InputMouseEventArgs e)
    {
        AnimateOpacity(0);
    }

    private void Surface_MouseMove(object sender, InputMouseEventArgs e)
    {
        MoveTo(e.GetPosition(_surface));
    }

    private void MoveTo(WindowsPoint cursorPosition)
    {
        var targetX = cursorPosition.X - _lightSize / 2;
        var targetY = cursorPosition.Y - _lightSize / 2;

        _transform.X = targetX;
        _transform.Y = targetY;
    }

    private void AnimateOpacity(double opacity)
    {
        var animation = new DoubleAnimation(opacity, FadeDuration)
        {
            EasingFunction = FadeEase
        };
        _light.BeginAnimation(UIElement.OpacityProperty, animation);
    }
}
