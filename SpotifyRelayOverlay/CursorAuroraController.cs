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

public sealed class CursorAuroraController : IDisposable
{
    private static readonly Duration FadeDuration = TimeSpan.FromMilliseconds(220);
    private static readonly IEasingFunction FadeEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
    private static readonly WpfColor SpotifyGreen = WpfColor.FromRgb(0x1E, 0xD7, 0x60);
    private static readonly WpfColor MintGreen = WpfColor.FromRgb(0x8D, 0xE6, 0xB1);

    private readonly FrameworkElement _surface;
    private readonly Action<WpfBrush> _setBackground;
    private readonly DrawingBrush _backgroundBrush;
    private readonly RectangleGeometry _surfaceGeometry = new();
    private readonly AuroraBand[] _bands;

    private CursorAuroraController(FrameworkElement surface, Action<WpfBrush> setBackground, WpfColor baseColor)
    {
        _surface = surface;
        _setBackground = setBackground;
        _bands =
        [
            new AuroraBand(baseColor, SpotifyGreen, 0.58, 0.34, 0.12, 245, 92, -18, 0, 0, -0.12, -0.05),
            new AuroraBand(baseColor, MintGreen, 0.30, 0.16, 0.06, 205, 78, 24, -92, 54, 0.10, 0.12),
            new AuroraBand(baseColor, SpotifyGreen, 0.22, 0.12, 0.045, 285, 105, 10, 118, -62, -0.16, 0.08),
            new AuroraBand(baseColor, SpotifyGreen, 0.16, 0.08, 0.03, 170, 64, -36, 42, 86, 0.06, -0.15)
        ];

        var drawingGroup = new DrawingGroup();
        drawingGroup.Children.Add(new GeometryDrawing(new SolidColorBrush(baseColor), null, _surfaceGeometry));
        foreach (var band in _bands)
        {
            drawingGroup.Children.Add(band.CreateDrawing(_surfaceGeometry));
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

    public static CursorAuroraController ForBorder(Border border, string baseColor)
    {
        return new CursorAuroraController(border, brush => border.Background = brush, ParseColor(baseColor));
    }

    public static CursorAuroraController ForPanel(WpfPanel panel, string baseColor)
    {
        return new CursorAuroraController(panel, brush => panel.Background = brush, ParseColor(baseColor));
    }

    public void Dispose()
    {
        _surface.SizeChanged -= Surface_SizeChanged;
        _surface.MouseEnter -= Surface_MouseEnter;
        _surface.MouseLeave -= Surface_MouseLeave;
        _surface.MouseMove -= Surface_MouseMove;
        foreach (var band in _bands)
        {
            band.ClearAnimations();
        }
    }

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBounds();
    }

    private void Surface_MouseEnter(object sender, InputMouseEventArgs e)
    {
        MoveTo(e.GetPosition(_surface));
        foreach (var band in _bands)
        {
            band.Show();
        }
    }

    private void Surface_MouseLeave(object sender, InputMouseEventArgs e)
    {
        foreach (var band in _bands)
        {
            band.Hide();
        }
    }

    private void Surface_MouseMove(object sender, InputMouseEventArgs e)
    {
        MoveTo(e.GetPosition(_surface));
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

    private void MoveTo(WindowsPoint cursorPosition)
    {
        var size = new WpfSize(Math.Max(1, _surface.ActualWidth), Math.Max(1, _surface.ActualHeight));
        var cursor = new WindowsPoint(
            Math.Clamp(cursorPosition.X, 0, size.Width),
            Math.Clamp(cursorPosition.Y, 0, size.Height));

        foreach (var band in _bands)
        {
            band.Move(cursor, size);
        }
    }

    private static WpfColor ParseColor(string value)
    {
        return (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(value)!;
    }

    private sealed class AuroraBand
    {
        private readonly RadialGradientBrush _brush;
        private readonly RotateTransform _rotation;
        private readonly GradientStop _coreStop;
        private readonly GradientStop _bodyStop;
        private readonly GradientStop _tailStop;
        private readonly WpfColor _hiddenCore;
        private readonly WpfColor _hiddenBody;
        private readonly WpfColor _hiddenTail;
        private readonly WpfColor _visibleCore;
        private readonly WpfColor _visibleBody;
        private readonly WpfColor _visibleTail;
        private readonly double _offsetX;
        private readonly double _offsetY;
        private readonly double _parallaxX;
        private readonly double _parallaxY;

        public AuroraBand(
            WpfColor baseColor,
            WpfColor targetColor,
            double coreStrength,
            double bodyStrength,
            double tailStrength,
            double radiusX,
            double radiusY,
            double angle,
            double offsetX,
            double offsetY,
            double parallaxX,
            double parallaxY)
        {
            _offsetX = offsetX;
            _offsetY = offsetY;
            _parallaxX = parallaxX;
            _parallaxY = parallaxY;
            _hiddenCore = ToTransparent(baseColor);
            _hiddenBody = ToTransparent(baseColor);
            _hiddenTail = ToTransparent(baseColor);
            _visibleCore = CreateAuroraColor(baseColor, targetColor, coreStrength, 0x9C);
            _visibleBody = CreateAuroraColor(baseColor, targetColor, bodyStrength, 0x58);
            _visibleTail = CreateAuroraColor(baseColor, targetColor, tailStrength, 0x24);
            _coreStop = new GradientStop(_hiddenCore, 0);
            _bodyStop = new GradientStop(_hiddenBody, 0.42);
            _tailStop = new GradientStop(_hiddenTail, 0.82);
            _rotation = new RotateTransform(angle);
            _brush = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                Center = new WindowsPoint(0, 0),
                GradientOrigin = new WindowsPoint(0, 0),
                RadiusX = radiusX,
                RadiusY = radiusY,
                Transform = _rotation
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

        public void Move(WindowsPoint cursor, WpfSize size)
        {
            var normalX = cursor.X / size.Width - 0.5;
            var normalY = cursor.Y / size.Height - 0.5;
            var center = new WindowsPoint(
                cursor.X + _offsetX + normalX * size.Width * _parallaxX,
                cursor.Y + _offsetY + normalY * size.Height * _parallaxY);

            _brush.Center = center;
            _brush.GradientOrigin = new WindowsPoint(
                center.X - normalX * 32,
                center.Y - normalY * 24);
            _rotation.CenterX = center.X;
            _rotation.CenterY = center.Y;
        }

        public void Show()
        {
            Animate(_coreStop, _visibleCore);
            Animate(_bodyStop, _visibleBody);
            Animate(_tailStop, _visibleTail);
        }

        public void Hide()
        {
            Animate(_coreStop, _hiddenCore);
            Animate(_bodyStop, _hiddenBody);
            Animate(_tailStop, _hiddenTail);
        }

        public void ClearAnimations()
        {
            ClearAnimation(_coreStop);
            ClearAnimation(_bodyStop);
            ClearAnimation(_tailStop);
        }

        private static void Animate(GradientStop stop, WpfColor color)
        {
            var animation = new ColorAnimation(color, FadeDuration)
            {
                EasingFunction = FadeEase
            };
            stop.BeginAnimation(GradientStop.ColorProperty, animation);
        }

        private static void ClearAnimation(GradientStop stop)
        {
            stop.BeginAnimation(GradientStop.ColorProperty, null);
        }

        private static WpfColor CreateAuroraColor(WpfColor baseColor, WpfColor targetColor, double amount, byte alpha)
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
