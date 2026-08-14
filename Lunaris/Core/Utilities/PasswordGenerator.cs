using System.Security.Cryptography;
using System.Text;

namespace Lunaris.Core.Utilities;

/// <summary>
/// Cryptographically secure random password generator (RandomNumberGenerator, never Random).
/// </summary>
public static class PasswordGenerator
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{};:,.?";

    public static string Generate(int length, bool includeUppercase = true, bool includeLowercase = true, bool includeDigits = true, bool includeSymbols = true)
    {
        if (length < 4)
            length = 4;
        if (length > 256)
            length = 256;

        var pool = new StringBuilder();
        if (includeUppercase) pool.Append(Uppercase);
        if (includeLowercase) pool.Append(Lowercase);
        if (includeDigits) pool.Append(Digits);
        if (includeSymbols) pool.Append(Symbols);
        if (pool.Length == 0)
            pool.Append(Lowercase);

        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        for (var i = 0; i < length; i++)
            result[i] = pool[bytes[i] % pool.Length];

        return new string(result);
    }
}