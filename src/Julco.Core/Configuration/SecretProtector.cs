using System.Security.Cryptography;
using System.Text;

namespace Julco.Core.Configuration;

public static class SecretProtector
{
    private const string Prefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Julco.Settings.Secret.v1");

    public static bool IsProtected(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.StartsWith(Prefix, StringComparison.Ordinal);
    }

    public static string Protect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (IsProtected(trimmed))
        {
            return trimmed;
        }

        if (!OperatingSystem.IsWindows())
        {
            return trimmed;
        }

        var bytes = Encoding.UTF8.GetBytes(trimmed);
        var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (!IsProtected(trimmed))
        {
            return trimmed;
        }

        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(trimmed[Prefix.Length..]);
            var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
