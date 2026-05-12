using System.Security.Cryptography;
using System.Text;

namespace SpotifyFavoritesTool;

internal static class WindowsDataProtection
{
    private const string Prefix = "dpapi:";
    private static readonly byte[] CurrentEntropy = Encoding.UTF8.GetBytes("SpotifyFavoritesTool.Settings.v1");
    private static readonly byte[] LegacyEntropy = Encoding.UTF8.GetBytes("SpotifyRelayOverlay.Settings.v1");

    public static string Protect(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(bytes, CurrentEntropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        byte[] protectedBytes;
        try
        {
            protectedBytes = Convert.FromBase64String(value[Prefix.Length..]);
        }
        catch
        {
            return string.Empty;
        }

        return TryUnprotect(protectedBytes, CurrentEntropy)
            ?? TryUnprotect(protectedBytes, LegacyEntropy)
            ?? string.Empty;
    }

    private static string? TryUnprotect(byte[] protectedBytes, byte[] entropy)
    {
        try
        {
            var bytes = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }
}
