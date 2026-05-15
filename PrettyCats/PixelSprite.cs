using System.Windows;
using System.Windows.Media;

namespace PrettyCats;

internal enum CatPixel
{
    Body,
    Dark,
    Light,
    Eye,
    Tongue,
    Sleep,
    Shadow
}

internal readonly record struct PixelRun(int X, int Y, int Width, int Height, CatPixel Color);

internal sealed class PixelSprite
{
    private readonly PixelRun[] _runs;

    public PixelSprite(params PixelRun[] runs)
    {
        _runs = runs;
    }

    public void Draw(DrawingContext drawingContext, Point origin, double pixelSize, int direction, CatPalette palette)
    {
        foreach (var run in _runs)
        {
            var left = direction >= 0
                ? origin.X + run.X * pixelSize
                : origin.X - (run.X + run.Width) * pixelSize;
            var top = origin.Y + run.Y * pixelSize;
            drawingContext.DrawRectangle(
                palette[run.Color],
                null,
                new Rect(
                    Math.Round(left),
                    Math.Round(top),
                    run.Width * pixelSize,
                    run.Height * pixelSize));
        }
    }
}
