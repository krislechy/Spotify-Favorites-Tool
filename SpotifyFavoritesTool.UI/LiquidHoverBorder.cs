using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace SpotifyFavoritesTool.UI;

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

    public static readonly DependencyProperty IsLiquidEnabledProperty =
        DependencyProperty.Register(
            nameof(IsLiquidEnabled),
            typeof(bool),
            typeof(LiquidHoverBorder),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SurfaceOpacityProperty =
        DependencyProperty.Register(
            nameof(SurfaceOpacity),
            typeof(double),
            typeof(LiquidHoverBorder),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnSurfaceOpacityChanged));

    private static readonly WpfColor SpotifyGreen = WpfColor.FromRgb(0x1E, 0xD7, 0x60);
    private static readonly WpfColor MintGreen = WpfColor.FromRgb(0x8D, 0xE6, 0xB1);
    private static readonly WpfColor DeepGreen = WpfColor.FromRgb(0x10, 0x8B, 0x46);

    private static readonly LiquidBlobSpec[] BlobSpecs =
    [
        new(0.00, 0.00, 0.00, 0.00, 190, 190, 20, 14, 0.90, 0.035, 0.46, 17, 4.4, 1.05, 34, 46, 24, 16, 2.60, 0.10, SpotifyGreen, 0.90, 0.52, 0.18, 0xB8, 0x70, 0x24),
        new(-78, 42, 0.030, -0.020, 135, 135, 16, 22, 1.12, 0.050, 0.34, 11, 2.9, 0.78, 24, 58, 18, 25, -3.35, 1.40, MintGreen, 0.60, 0.30, 0.11, 0x90, 0x54, 0x1E),
        new(86, -56, -0.040, 0.032, 150, 150, 24, 18, 0.74, 0.045, 0.58, 8.5, 2.4, 1.16, 42, 64, 28, 20, 2.95, 2.35, SpotifyGreen, 0.52, 0.27, 0.10, 0x88, 0x4C, 0x1C),
        new(22, 88, 0.020, 0.045, 230, 230, 18, 26, 0.58, 0.030, 0.30, 6.2, 1.8, 0.58, 18, 50, 14, 30, -2.15, 3.20, DeepGreen, 0.54, 0.25, 0.09, 0x72, 0x3C, 0x18),
        new(-24, -104, -0.018, 0.055, 255, 255, 28, 16, 0.66, 0.026, 0.40, 9.4, 3.6, 0.72, 28, 72, 32, 18, 3.70, 4.15, MintGreen, 0.30, 0.16, 0.07, 0x48, 0x2C, 0x14)
    ];

    private long _animationStartedAt;
    private WpfBrush? _baseBrush;
    private WpfBrush[]? _blobBrushes;
    private bool _isRendering;

    public LiquidHoverBorder()
    {
        Background = WpfBrushes.Transparent;
        Loaded += (_, _) =>
        {
            if (IsLiquidEnabled)
            {
                StartRendering();
            }
        };
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && IsLiquidEnabled)
            {
                StartRendering();
                return;
            }

            StopRendering();
        };
    }

    public WpfColor BaseColor
    {
        get => (WpfColor)GetValue(BaseColorProperty);
        set => SetValue(BaseColorProperty, value);
    }

    public bool IsLiquidEnabled
    {
        get => (bool)GetValue(IsLiquidEnabledProperty);
        set => SetValue(IsLiquidEnabledProperty, value);
    }

    public double SurfaceOpacity
    {
        get => (double)GetValue(SurfaceOpacityProperty);
        set => SetValue(SurfaceOpacityProperty, value);
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

        drawingContext.DrawRoundedRectangle(GetBaseBrush(), null, fillRect, fillRadius, fillRadius);

        if (IsLiquidEnabled)
        {
            var clip = new RectangleGeometry(fillRect, fillRadius, fillRadius);
            drawingContext.PushClip(clip);
            drawingContext.PushOpacity(GetLiquidOpacity());
            DrawLiquidGradient(drawingContext, fillRect, GetAnimationSeconds());
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

    private void DrawLiquidGradient(DrawingContext drawingContext, Rect bounds, double seconds)
    {
        var blobBrushes = GetBlobBrushes();
        var pointer = CreateAmbientPoint(bounds, seconds);

        var normalX = bounds.Width <= 0 ? 0 : pointer.X / bounds.Width - 0.5;
        var normalY = bounds.Height <= 0 ? 0 : pointer.Y / bounds.Height - 0.5;

        for (var index = 0; index < BlobSpecs.Length; index++)
        {
            var spec = BlobSpecs[index];
            const double pressure = 0;
            var phase = seconds * spec.Speed + index * 1.618;
            var driftX = Math.Sin(phase) * spec.DriftX
                + Math.Sin(phase * 0.47 + 1.4) * spec.DriftX * 0.38;
            var driftY = Math.Cos(phase * 0.82 + 0.7) * spec.DriftY
                + Math.Sin(phase * 0.39 + 2.1) * spec.DriftY * 0.32;
            var orbitPhase = seconds * spec.OrbitSpeed + spec.OrbitPhase;
            var orbitX = Math.Cos(orbitPhase) * spec.OrbitRadiusX
                + Math.Cos(orbitPhase * 1.83 + index) * spec.OrbitRadiusX * 0.22;
            var orbitY = Math.Sin(orbitPhase) * spec.OrbitRadiusY
                + Math.Sin(orbitPhase * 1.31 + index * 0.7) * spec.OrbitRadiusY * 0.22;
            var blurPhase = seconds * (Math.Abs(spec.OrbitSpeed) * 0.64 + spec.Speed * 0.52) + spec.OrbitPhase * 1.7;
            var anchorBlurX = pressure * spec.AnchorBlur
                * (Math.Cos(blurPhase) + Math.Sin(blurPhase * 1.53 + index) * 0.42);
            var anchorBlurY = pressure * spec.AnchorBlur
                * (Math.Sin(blurPhase * 0.91 + index * 0.6) + Math.Cos(blurPhase * 1.27) * 0.42);
            var pulse = 1 + Math.Sin(phase * 0.63 + index * 0.9) * spec.Pulse;
            var speedShrink = 1 - pressure * spec.SpeedShrink;
            var asynchronousWobble = 1 + pressure * Math.Sin(phase * 1.7 + index) * spec.Pulse * 2.7;
            var radiusScale = Math.Max(0.34, pulse * speedShrink * asynchronousWobble);
            var center = new WpfPoint(
                pointer.X + spec.OffsetX + driftX + orbitX + anchorBlurX + normalX * bounds.Width * spec.ParallaxX,
                pointer.Y + spec.OffsetY + driftY + orbitY + anchorBlurY + normalY * bounds.Height * spec.ParallaxY);

            drawingContext.DrawEllipse(
                blobBrushes[index],
                null,
                center,
                spec.RadiusX * radiusScale,
                spec.RadiusY * radiusScale);

        }
    }

    private WpfBrush GetBaseBrush()
    {
        if (_baseBrush is not null)
        {
            return _baseBrush;
        }

        var opacity = Math.Clamp(SurfaceOpacity, 0, 1);
        var alpha = (byte)Math.Round(BaseColor.A * opacity);
        var brush = new SolidColorBrush(WpfColor.FromArgb(alpha, BaseColor.R, BaseColor.G, BaseColor.B));
        brush.Freeze();
        _baseBrush = brush;
        return _baseBrush;
    }

    private double GetLiquidOpacity()
    {
        return Math.Clamp(SurfaceOpacity * 0.95, 0, 0.95);
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

    private void StartRendering()
    {
        if (_isRendering)
        {
            return;
        }

        CompositionTarget.Rendering += CompositionTarget_Rendering;
        _animationStartedAt = Stopwatch.GetTimestamp();
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
        InvalidateVisual();

        if (!IsLiquidEnabled || !IsVisible)
        {
            StopRendering();
        }
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
        var liquidHoverBorder = (LiquidHoverBorder)dependencyObject;
        liquidHoverBorder._baseBrush = null;
        liquidHoverBorder._blobBrushes = null;
    }

    private static void OnSurfaceOpacityChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var liquidHoverBorder = (LiquidHoverBorder)dependencyObject;
        liquidHoverBorder._baseBrush = null;
    }

    private static WpfPoint CreateAmbientPoint(Rect bounds, double seconds)
    {
        var centerX = bounds.Left + bounds.Width / 2;
        var centerY = bounds.Top + bounds.Height / 2;
        return new WpfPoint(
            centerX
                + Math.Sin(seconds * 0.31) * bounds.Width * 0.18
                + Math.Cos(seconds * 0.17 + 1.2) * bounds.Width * 0.11,
            centerY
                + Math.Cos(seconds * 0.27 + 0.8) * bounds.Height * 0.19
                + Math.Sin(seconds * 0.14 + 2.4) * bounds.Height * 0.10);
    }

    private double GetAnimationSeconds()
    {
        if (_animationStartedAt == 0)
        {
            _animationStartedAt = Stopwatch.GetTimestamp();
        }

        return Stopwatch.GetElapsedTime(_animationStartedAt).TotalSeconds;
    }

    private readonly record struct LiquidBlobSpec(
        double OffsetX,
        double OffsetY,
        double ParallaxX,
        double ParallaxY,
        double RadiusX,
        double RadiusY,
        double DriftX,
        double DriftY,
        double Speed,
        double Pulse,
        double SpeedShrink,
        double SpeedAttack,
        double SpeedRelease,
        double MotionGlow,
        double MotionTrail,
        double AnchorBlur,
        double OrbitRadiusX,
        double OrbitRadiusY,
        double OrbitSpeed,
        double OrbitPhase,
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
            return CreateBrush(baseColor, CoreStrength, BodyStrength, TailStrength, CoreAlpha, BodyAlpha, TailAlpha);
        }

        private WpfBrush CreateBrush(
            WpfColor baseColor,
            double coreStrength,
            double bodyStrength,
            double tailStrength,
            byte coreAlpha,
            byte bodyAlpha,
            byte tailAlpha)
        {
            var brush = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
                Center = new WpfPoint(0.5, 0.5),
                GradientOrigin = new WpfPoint(0.42, 0.42),
                RadiusX = 0.58,
                RadiusY = 0.58
            };
            brush.GradientStops.Add(new GradientStop(CreateColor(baseColor, Math.Min(1, coreStrength + 0.16), AddAlpha(coreAlpha, 0x26)), 0));
            brush.GradientStops.Add(new GradientStop(CreateColor(baseColor, Math.Min(1, bodyStrength + 0.14), AddAlpha(bodyAlpha, 0x22)), 0.20));
            brush.GradientStops.Add(new GradientStop(CreateColor(baseColor, Math.Min(1, tailStrength + 0.12), AddAlpha(tailAlpha, 0x18)), 0.48));
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

        private static byte AddAlpha(byte current, byte amount)
        {
            return (byte)Math.Min(byte.MaxValue, current + amount);
        }
    }
}
