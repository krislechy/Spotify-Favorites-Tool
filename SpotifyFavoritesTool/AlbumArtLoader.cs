using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace SpotifyFavoritesTool;

internal static class AlbumArtLoader
{
    private static readonly HttpClient HttpClient = new();

    public static async Task<BitmapImage?> LoadAsync(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        var bytes = await HttpClient.GetByteArrayAsync(imageUrl);
        using var stream = new MemoryStream(bytes);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();

        if (image.CanFreeze)
        {
            image.Freeze();
        }

        return image;
    }
}
