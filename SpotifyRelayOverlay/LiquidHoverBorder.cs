using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace SpotifyRelayOverlay;

public class LiquidHoverBorder : Border
{
    public static readonly DependencyProperty BaseColorProperty =
        DependencyProperty.Register(
            nameof(BaseColor),
            typeof(WpfColor),
            typeof(LiquidHoverBorder),
            new FrameworkPropertyMetadata(
                (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString("#141B17")!,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnBaseColorChanged));

    private static readonly DependencyProperty HoverProgressProperty =
        DependencyProperty.Register(
            nameof(HoverProgress),
            typeof(double),
            typeof(LiquidHoverBorder),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Duration HoverFadeDuration = TimeSpan.FromMilliseconds(180);
    private static readonly IEasingFunction HoverFadeEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
    private static readonly WpfColor SpotifyGreen = WpfColor.FromRgb(0x1E, 0xD7, 0x60);
    private static readonly WpfColor MintGreen = WpfColor.FromRgb(0x8D, 0xE6, 0xB1);
    private static readonly WpfColor DeepGreen = WpfColor.FromRgb(0x10, 0x8B, 0x46);

    private static readonly LiquidBlobSpec[] BlobSpecs =
    [
        new(0.00, 0.00, 0.00, 0.00, 190, 190, SpotifyGreen, 0.90, 0.52, 0.18, 0xB8, 0x70, 0x24),
        new(-78, 42, 0.030, -0.020, 135, 135, MintGreen, 0.60, 0.30, 0.11, 0x90, 0x54, 0x1E),
        new(86, -56, -0.040, 0.032, 150, 150, SpotifyGreen, 0.52, 0.27, 0.10, 0x88, 0x4C, 0x1C),
        new(22, 88, 0.020, 0.045, 230, 230, DeepGreen, 0.54, 0.25, 0.09, 0x72, 0x3C, 0x18),
        new(-24, -104, -0.018, 0.055, 255, 255, MintGreen, 0.30, 0.16, 0.07, 0x48, 0x2C, 0x14)
    ];

    private WpfPoint _targetPoint;
    private WpfPoint _currentPoint;
    private WpfBrush[]? _blobBrushes;
    private bool _hasPoint;
    private bool _isRendering;

    public LiquidHoverBorder()
    {
        Background = WpfBrushes.Transparent;
    }

    public WpfColor BaseColor
    {
        get => (WpfColor)GetValue(BaseColorProperty);
        set => SetValue(BaseColorProperty, value);
    }

    private double HoverProgress
    {
        get => (double)GetValue(HoverProgressProperty);
        set => SetValue(HoverProgressProperty, value);
    }

    protected override void OnMouseEnter(WpfMouseEventArgs e)
    {
        base.OnMouseEnter(e);
        SetPointer(e.GetPosition(this), snap: true);
        AnimateHover(1);
        StartRendering();
    }

    protected override void OnMouseMove(WpfMouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetPointer(e.GetPosition(this), snap: false);
        StartRendering();
    }

    protected override void OnMouseLeave(WpfMouseEventArgs e)
    {
        base.OnMouseLeave(e);
        AnimateHover(0);
        StartRendering();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var borderThickness = GetUniformBorderThickness();
        var radius = GetUniformCornerRadius();
        var borderPen = CreateBorderPen(borderThickness);
        var drawRect = Deflate(bounds, borderThickness / 2);
        var fillRect = Deflate(bounds, borderThickness);
        var fillRadius = Math.Max(0, radius - borderThickness / 2);

        drawingContext.DrawRoundedRectangle(new SolidColorBrush(BaseColor), null, fillRect, fillRadius, fillRadius);

        var hoverProgress = HoverProgress;
        if (hoverProgress > 0.001)
        {
            var clip = new RectangleGeometry(fillRect, fillRadius, fillRadius);
            drawingContext.PushClip(clip);
            drawingContext.PushOpacity(hoverProgress);
            DrawLiquidGradient(drawingContext, fillRect);
            drawingContext.Pop();
            drawingContext.Pop();
        }

        if (borderPen is not null)
        {
            drawingContext.DrawRoundedRectangle(null, borderPen, drawRect, radius, radius);
        }
    }

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        if (VisualParent is null)
        {
            StopRendering();
        }
    }

    private void DrawLiquidGradient(DrawingContext drawingContext, Rect bounds)
    {
        var blobBrushes = GetBlobBrushes();
        var pointer = _hasPoint
            ? _currentPoint
            : new WpfPoint(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);

        var normalX = bounds.Width <= 0 ? 0 : pointer.X / bounds.Width - 0.5;
        var normalY = bounds.Height <= 0 ? 0 : pointer.Y / bounds.Height - 0.5;

        for (var index = 0; index < BlobSpecs.Length; index++)
        {
            var spec = BlobSpecs[index];
            var center = new WpfPoint(
                pointer.X + spec.OffsetX + normalX * bounds.Width * spec.ParallaxX,
                pointer.Y + spec.OffsetY + normalY * bounds.Height * spec.ParallaxY);

            drawingContext.DrawEllipse(
                blobBrushes[index],
                null,
                center,
                spec.RadiusX,
                spec.RadiusY);
        }
    }

    private WpfBrush[] GetBlobBrushes()
    {
        if (_blobBrushes is not null)
        {
            return _blobBrushes;
        }

        _blobBrushes = BlobSpecs
            .Select(spec => spec.CreateBrush(BaseColor))
            .ToArray();
        return _blobBrushes;
    }

    private void SetPointer(WpfPoint point, bool snap)
    {
        _targetPoint = new WpfPoint(
            Math.Clamp(point.X, 0, Math.Max(1, ActualWidth)),
            Math.Clamp(point.Y, 0, Math.Max(1, ActualHeight)));

        if (!_hasPoint || snap)
        {
            _currentPoint = _targetPoint;
            _hasPoint = true;
        }
    }

    private void AnimateHover(double target)
    {
        var animation = new DoubleAnimation(target, HoverFadeDuration)
        {
            EasingFunction = HoverFadeEase
        };
        BeginAnimation(HoverProgressProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void StartRendering()
    {
        if (_isRendering)
        {
            return;
        }

        CompositionTarget.Rendering += CompositionTarget_Rendering;
        _isRendering = true;
    }

    private void StopRendering()
    {
        if (!_isRendering)
        {
            return;
        }

        CompositionTarget.Rendering -= CompositionTarget_Rendering;
        _isRendering = false;
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        var distanceX = _targetPoint.X - _currentPoint.X;
        var distanceY = _targetPoint.Y - _currentPoint.Y;
        var distance = Math.Sqrt(distanceX * distanceX + distanceY * distanceY);

        if (distance > 0.15)
        {
            _currentPoint = new WpfPoint(
                _currentPoint.X + distanceX * 0.18,
                _currentPoint.Y + distanceY * 0.18);
            InvalidateVisual();
            return;
        }

        _currentPoint = _targetPoint;
        InvalidateVisual();
        StopRendering();
    }

    private WpfPen? CreateBorderPen(double thickness)
    {
        if (thickness <= 0 || BorderBrush is null)
        {
            return null;
        }

        return new WpfPen(BorderBrush, thickness);
    }

    private double GetUniformBorderThickness()
    {
        var thickness = BorderThickness;
        return Math.Max(0, (thickness.Left + thickness.Top + thickness.Right + thickness.Bottom) / 4);
    }

    private double GetUniformCornerRadius()
    {
        var corner = CornerRadius;
        return Math.Max(0, (corner.TopLeft + corner.TopRight + corner.BottomRight + corner.BottomLeft) / 4);
    }

    private static Rect Deflate(Rect rect, double amount)
    {
        if (amount <= 0)
        {
            return rect;
        }

        return new Rect(
            rect.Left + amount,
            rect.Top + amount,
            Math.Max(0, rect.Width - amount * 2),
            Math.Max(0, rect.Height - amount * 2));
    }

    private static void OnBaseColorChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((LiquidHoverBorder)dependencyObject)._blobBrushes = null;
    }

    private readonly record struct LiquidBlobSpec(
        double OffsetX,
        double OffsetY,
        double ParallaxX,
        double ParallaxY,
        double RadiusX,
        double RadiusY,
        WpfColor TargetColor,
        double CoreStrength,
        double BodyStrength,
        double TailStrength,
        byte CoreAlpha,
        byte BodyAlpha,
        byte TailAlpha)
    {
        public WpfBrush CreateBrush(WpfColor baseColor)
        {
            var brush = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
                Center = new WpfPoint(0.5, 0.5),
                GradientOrigin = new WpfPoint(0.42, 0.42),
                RadiusX = 0.74,
                RadiusY = 0.74
            };
            brush.GradientStops.Add(new GradientStop(CreateColor(baseColor, CoreStrength, CoreAlpha), 0));
            brush.GradientStops.Add(new GradientStop(CreateColor(baseColor, BodyStrength, BodyAlpha), 0.34));
            brush.GradientStops.Add(new GradientStop(CreateColor(baseColor, TailStrength, TailAlpha), 0.72));
            brush.GradientStops.Add(new GradientStop(WpfColor.FromArgb(0, baseColor.R, baseColor.G, baseColor.B), 1));
            brush.Freeze();
            return brush;
        }

        private WpfColor CreateColor(WpfColor baseColor, double strength, byte alpha)
        {
            var color = WpfColor.FromRgb(
                BlendChannel(baseColor.R, TargetColor.R, strength),
                BlendChannel(baseColor.G, TargetColor.G, strength),
                BlendChannel(baseColor.B, TargetColor.B, strength));
            return WpfColor.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static byte BlendChannel(byte current, byte target, double amount)
        {
            return (byte)Math.Round(current + (target - current) * amount);
        }
    }
}
