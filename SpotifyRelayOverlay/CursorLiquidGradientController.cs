using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InputMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPanel = System.Windows.Controls.Panel;
using WpfSize = System.Windows.Size;
using WindowsPoint = System.Windows.Point;

namespace SpotifyRelayOverlay;

public sealed class CursorLiquidGradientController : IDisposable
{
    private static readonly Duration FadeDuration = TimeSpan.FromMilliseconds(190);
    private static readonly Duration MoveDuration = TimeSpan.FromMilliseconds(120);
    private static readonly IEasingFunction FadeEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction MoveEase = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly WpfColor SpotifyGreen = WpfColor.FromRgb(0x1E, 0xD7, 0x60);
    private static readonly WpfColor MintGreen = WpfColor.FromRgb(0x8D, 0xE6, 0xB1);
    private static readonly WpfColor DeepGreen = WpfColor.FromRgb(0x10, 0x8B, 0x46);

    private readonly FrameworkElement _surface;
    private readonly Action<WpfBrush> _setBackground;
    private readonly DrawingBrush _backgroundBrush;
    private readonly RectangleGeometry _surfaceGeometry = new();
    private readonly LiquidBlob[] _blobs;

    private CursorLiquidGradientController(FrameworkElement surface, Action<WpfBrush> setBackground, WpfColor baseColor)
    {
        _surface = surface;
        _setBackground = setBackground;
        _blobs =
        [
            new LiquidBlob(baseColor, SpotifyGreen, 0.84, 0.42, 0.12, 172, 0, 0, 0.00, 0.00, 22, -10),
            new LiquidBlob(baseColor, MintGreen, 0.54, 0.24, 0.075, 128, -72, 38, 0.045, -0.020, -18, 14),
            new LiquidBlob(baseColor, SpotifyGreen, 0.40, 0.18, 0.055, 146, 86, -52, -0.055, 0.040, 12, 20),
            new LiquidBlob(baseColor, DeepGreen, 0.50, 0.22, 0.060, 205, 22, 74, 0.030, 0.055, -28, -8),
            new LiquidBlob(baseColor, MintGreen, 0.24, 0.12, 0.040, 235, -18, -96, -0.025, 0.070, 8, -24)
        ];

        var drawingGroup = new DrawingGroup();
        drawingGroup.Children.Add(new GeometryDrawing(new SolidColorBrush(baseColor), null, _surfaceGeometry));
        foreach (var blob in _blobs)
        {
            drawingGroup.Children.Add(blob.CreateDrawing(_surfaceGeometry));
        }

        _backgroundBrush = new DrawingBrush(drawingGroup)
        {
            Stretch = Stretch.None,
            TileMode = TileMode.None,
            ViewboxUnits = BrushMappingMode.Absolute,
            ViewportUnits = BrushMappingMode.Absolute
        };
        _setBackground(_backgroundBrush);
        UpdateBounds();

        _surface.SizeChanged += Surface_SizeChanged;
        _surface.MouseEnter += Surface_MouseEnter;
        _surface.MouseLeave += Surface_MouseLeave;
        _surface.MouseMove += Surface_MouseMove;
    }

    public static CursorLiquidGradientController ForBorder(Border border, string baseColor)
    {
        return new CursorLiquidGradientController(border, brush => border.Background = brush, ParseColor(baseColor));
    }

    public static CursorLiquidGradientController ForPanel(WpfPanel panel, string baseColor)
    {
        return new CursorLiquidGradientController(panel, brush => panel.Background = brush, ParseColor(baseColor));
    }

    public void Dispose()
    {
        _surface.SizeChanged -= Surface_SizeChanged;
        _surface.MouseEnter -= Surface_MouseEnter;
        _surface.MouseLeave -= Surface_MouseLeave;
        _surface.MouseMove -= Surface_MouseMove;
        foreach (var blob in _blobs)
        {
            blob.ClearAnimations();
        }
    }

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBounds();
    }

    private void Surface_MouseEnter(object sender, InputMouseEventArgs e)
    {
        MoveTo(e.GetPosition(_surface), animate: false);
        foreach (var blob in _blobs)
        {
            blob.Show();
        }
    }

    private void Surface_MouseLeave(object sender, InputMouseEventArgs e)
    {
        foreach (var blob in _blobs)
        {
            blob.Hide();
        }
    }

    private void Surface_MouseMove(object sender, InputMouseEventArgs e)
    {
        MoveTo(e.GetPosition(_surface), animate: true);
    }

    private void UpdateBounds()
    {
        var width = Math.Max(1, _surface.ActualWidth);
        var height = Math.Max(1, _surface.ActualHeight);
        var bounds = new Rect(0, 0, width, height);
        _surfaceGeometry.Rect = bounds;
        _backgroundBrush.Viewbox = bounds;
        _backgroundBrush.Viewport = bounds;
    }

    private void MoveTo(WindowsPoint cursorPosition, bool animate)
    {
        var size = new WpfSize(Math.Max(1, _surface.ActualWidth), Math.Max(1, _surface.ActualHeight));
        var cursor = new WindowsPoint(
            Math.Clamp(cursorPosition.X, 0, size.Width),
            Math.Clamp(cursorPosition.Y, 0, size.Height));

        foreach (var blob in _blobs)
        {
            blob.Move(cursor, size, animate);
        }
    }

    private static WpfColor ParseColor(string value)
    {
        return (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(value)!;
    }

    private sealed class LiquidBlob
    {
        private readonly RadialGradientBrush _brush;
        private readonly GradientStop _coreStop;
        private readonly GradientStop _bodyStop;
        private readonly GradientStop _tailStop;
        private readonly WpfColor _hiddenCore;
        private readonly WpfColor _hiddenBody;
        private readonly WpfColor _hiddenTail;
        private readonly WpfColor _visibleCore;
        private readonly WpfColor _visibleBody;
        private readonly WpfColor _visibleTail;
        private readonly double _radius;
        private readonly double _offsetX;
        private readonly double _offsetY;
        private readonly double _parallaxX;
        private readonly double _parallaxY;
        private readonly double _originDriftX;
        private readonly double _originDriftY;

        public LiquidBlob(
            WpfColor baseColor,
            WpfColor targetColor,
            double coreStrength,
            double bodyStrength,
            double tailStrength,
            double radius,
            double offsetX,
            double offsetY,
            double parallaxX,
            double parallaxY,
            double originDriftX,
            double originDriftY)
        {
            _radius = radius;
            _offsetX = offsetX;
            _offsetY = offsetY;
            _parallaxX = parallaxX;
            _parallaxY = parallaxY;
            _originDriftX = originDriftX;
            _originDriftY = originDriftY;
            _hiddenCore = ToTransparent(baseColor);
            _hiddenBody = ToTransparent(baseColor);
            _hiddenTail = ToTransparent(baseColor);
            _visibleCore = CreateLiquidColor(baseColor, targetColor, coreStrength, 0xD6);
            _visibleBody = CreateLiquidColor(baseColor, targetColor, bodyStrength, 0x72);
            _visibleTail = CreateLiquidColor(baseColor, targetColor, tailStrength, 0x2C);
            _coreStop = new GradientStop(_hiddenCore, 0);
            _bodyStop = new GradientStop(_hiddenBody, 0.36);
            _tailStop = new GradientStop(_hiddenTail, 0.74);
            _brush = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                Center = new WindowsPoint(0, 0),
                GradientOrigin = new WindowsPoint(0, 0),
                RadiusX = _radius,
                RadiusY = _radius
            };
            _brush.GradientStops.Add(_coreStop);
            _brush.GradientStops.Add(_bodyStop);
            _brush.GradientStops.Add(_tailStop);
            _brush.GradientStops.Add(new GradientStop(ToTransparent(baseColor), 1));
        }

        public GeometryDrawing CreateDrawing(Geometry geometry)
        {
            return new GeometryDrawing(_brush, null, geometry);
        }

        public void Move(WindowsPoint cursor, WpfSize size, bool animate)
        {
            var normalX = cursor.X / size.Width - 0.5;
            var normalY = cursor.Y / size.Height - 0.5;
            var center = new WindowsPoint(
                cursor.X + _offsetX + normalX * size.Width * _parallaxX,
                cursor.Y + _offsetY + normalY * size.Height * _parallaxY);
            var origin = new WindowsPoint(
                center.X + _originDriftX - normalX * 26,
                center.Y + _originDriftY - normalY * 22);

            if (animate)
            {
                AnimatePoint(RadialGradientBrush.CenterProperty, center);
                AnimatePoint(RadialGradientBrush.GradientOriginProperty, origin);
                return;
            }

            _brush.BeginAnimation(RadialGradientBrush.CenterProperty, null);
            _brush.BeginAnimation(RadialGradientBrush.GradientOriginProperty, null);
            _brush.Center = center;
            _brush.GradientOrigin = origin;
        }

        public void Show()
        {
            AnimateColor(_coreStop, _visibleCore);
            AnimateColor(_bodyStop, _visibleBody);
            AnimateColor(_tailStop, _visibleTail);
        }

        public void Hide()
        {
            AnimateColor(_coreStop, _hiddenCore);
            AnimateColor(_bodyStop, _hiddenBody);
            AnimateColor(_tailStop, _hiddenTail);
        }

        public void ClearAnimations()
        {
            ClearAnimation(_coreStop);
            ClearAnimation(_bodyStop);
            ClearAnimation(_tailStop);
            _brush.BeginAnimation(RadialGradientBrush.CenterProperty, null);
            _brush.BeginAnimation(RadialGradientBrush.GradientOriginProperty, null);
        }

        private void AnimatePoint(DependencyProperty property, WindowsPoint target)
        {
            var animation = new PointAnimation(target, MoveDuration)
            {
                EasingFunction = MoveEase
            };
            _brush.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private static void AnimateColor(GradientStop stop, WpfColor color)
        {
            var animation = new ColorAnimation(color, FadeDuration)
            {
                EasingFunction = FadeEase
            };
            stop.BeginAnimation(GradientStop.ColorProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private static void ClearAnimation(GradientStop stop)
        {
            stop.BeginAnimation(GradientStop.ColorProperty, null);
        }

        private static WpfColor CreateLiquidColor(WpfColor baseColor, WpfColor targetColor, double amount, byte alpha)
        {
            var color = WpfColor.FromRgb(
                BlendChannel(baseColor.R, targetColor.R, amount),
                BlendChannel(baseColor.G, targetColor.G, amount),
                BlendChannel(baseColor.B, targetColor.B, amount));
            return WpfColor.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static WpfColor ToTransparent(WpfColor color)
        {
            return WpfColor.FromArgb(0, color.R, color.G, color.B);
        }

        private static byte BlendChannel(byte current, byte target, double amount)
        {
            return (byte)Math.Round(current + (target - current) * amount);
        }
    }
}
