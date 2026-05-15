namespace PrettyCats;

internal static class CatSprites
{
    private static readonly PixelSprite WalkA = new(
        new(2, 13, 18, 1, CatPixel.Shadow),
        new(2, 8, 4, 1, CatPixel.Body),
        new(0, 7, 2, 2, CatPixel.Body),
        new(0, 6, 1, 1, CatPixel.Dark),
        new(4, 6, 13, 6, CatPixel.Body),
        new(6, 5, 9, 1, CatPixel.Body),
        new(5, 11, 9, 1, CatPixel.Light),
        new(5, 6, 2, 1, CatPixel.Dark),
        new(11, 6, 2, 1, CatPixel.Dark),
        new(4, 12, 2, 2, CatPixel.Body),
        new(10, 12, 2, 2, CatPixel.Body),
        new(16, 11, 2, 2, CatPixel.Body),
        new(15, 4, 8, 7, CatPixel.Body),
        new(16, 2, 2, 3, CatPixel.Body),
        new(20, 2, 2, 3, CatPixel.Body),
        new(17, 7, 4, 1, CatPixel.Light),
        new(17, 6, 1, 1, CatPixel.Eye),
        new(20, 6, 1, 1, CatPixel.Eye),
        new(22, 7, 1, 1, CatPixel.Dark));

    private static readonly PixelSprite WalkB = new(
        new(2, 13, 18, 1, CatPixel.Shadow),
        new(2, 7, 4, 1, CatPixel.Body),
        new(0, 6, 2, 2, CatPixel.Body),
        new(0, 5, 1, 1, CatPixel.Dark),
        new(4, 6, 13, 6, CatPixel.Body),
        new(6, 5, 9, 1, CatPixel.Body),
        new(5, 11, 9, 1, CatPixel.Light),
        new(5, 6, 2, 1, CatPixel.Dark),
        new(11, 6, 2, 1, CatPixel.Dark),
        new(3, 12, 2, 2, CatPixel.Body),
        new(9, 12, 2, 2, CatPixel.Body),
        new(17, 11, 2, 2, CatPixel.Body),
        new(15, 4, 8, 7, CatPixel.Body),
        new(16, 2, 2, 3, CatPixel.Body),
        new(20, 2, 2, 3, CatPixel.Body),
        new(17, 7, 4, 1, CatPixel.Light),
        new(17, 6, 1, 1, CatPixel.Eye),
        new(20, 6, 1, 1, CatPixel.Eye),
        new(22, 7, 1, 1, CatPixel.Dark));

    private static readonly PixelSprite Groom = new(
        new(2, 13, 18, 1, CatPixel.Shadow),
        new(2, 8, 4, 1, CatPixel.Body),
        new(0, 7, 2, 2, CatPixel.Body),
        new(0, 6, 1, 1, CatPixel.Dark),
        new(4, 6, 13, 6, CatPixel.Body),
        new(6, 5, 9, 1, CatPixel.Body),
        new(5, 11, 9, 1, CatPixel.Light),
        new(7, 7, 3, 5, CatPixel.Light),
        new(15, 4, 8, 7, CatPixel.Body),
        new(16, 2, 2, 3, CatPixel.Body),
        new(20, 2, 2, 3, CatPixel.Body),
        new(17, 7, 4, 1, CatPixel.Light),
        new(17, 6, 1, 1, CatPixel.Eye),
        new(20, 6, 1, 1, CatPixel.Eye),
        new(17, 10, 2, 1, CatPixel.Tongue),
        new(4, 12, 2, 2, CatPixel.Body),
        new(13, 12, 2, 2, CatPixel.Body));

    private static readonly PixelSprite Sleep = new(
        new(2, 13, 18, 1, CatPixel.Shadow),
        new(2, 9, 4, 1, CatPixel.Body),
        new(0, 8, 2, 2, CatPixel.Body),
        new(4, 7, 13, 5, CatPixel.Body),
        new(6, 6, 9, 1, CatPixel.Body),
        new(5, 11, 9, 1, CatPixel.Light),
        new(15, 5, 8, 6, CatPixel.Body),
        new(16, 3, 2, 3, CatPixel.Body),
        new(20, 3, 2, 3, CatPixel.Body),
        new(17, 7, 4, 1, CatPixel.Dark),
        new(4, 12, 3, 1, CatPixel.Body),
        new(12, 12, 3, 1, CatPixel.Body));

    public static PixelSprite Get(CatState state, int frame)
    {
        return state switch
        {
            CatState.Grooming => Groom,
            CatState.Sleeping => Sleep,
            _ => (frame / 3) % 2 == 0 ? WalkA : WalkB
        };
    }
}
