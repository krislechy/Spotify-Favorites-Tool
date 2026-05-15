using System.Windows.Media;

namespace PrettyCats;

internal sealed class CatPalette
{
    private readonly Brush[] _brushes;

    private CatPalette(Color body, Color dark, Color light)
    {
        _brushes =
        [
            CreateBrush(body),
            CreateBrush(dark),
            CreateBrush(light),
            CreateBrush(Colors.Black),
            CreateBrush(Color.FromRgb(255, 128, 160)),
            CreateBrush(Color.FromRgb(198, 244, 255)),
            CreateBrush(Color.FromArgb(150, 0, 0, 0))
        ];
    }

    public Brush this[CatPixel color] => _brushes[(int)color];

    public static CatPalette Create(int index)
    {
        return index switch
        {
            0 => new CatPalette(
                Color.FromRgb(238, 166, 84),
                Color.FromRgb(128, 76, 42),
                Color.FromRgb(255, 211, 145)),
            1 => new CatPalette(
                Color.FromRgb(88, 98, 114),
                Color.FromRgb(42, 47, 56),
                Color.FromRgb(177, 188, 202)),
            _ => new CatPalette(
                Color.FromRgb(234, 235, 218),
                Color.FromRgb(112, 121, 111),
                Color.FromRgb(255, 248, 220))
        };
    }

    private static Brush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
