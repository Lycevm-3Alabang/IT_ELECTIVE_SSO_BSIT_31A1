using System.Security.Cryptography;

namespace Gateway.Areas.Admin.Services;

public static class TemporaryPasswordGenerator
{
    // Look-alike characters (I, l, O, 0, 1) are left out so the password is easy to read out loud.
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%";
    private const string All = Upper + Lower + Digits + Symbols;

    public static string Generate(int length = 12)
    {
        if (length < 4) length = 4;

        var chars = new List<char>(length)
        {
            // One from each class guarantees Identity's default password rules are met.
            Pick(Upper),
            Pick(Lower),
            Pick(Digits),
            Pick(Symbols)
        };

        while (chars.Count < length)
        {
            chars.Add(Pick(All));
        }

        // Shuffle so the guaranteed characters aren't always at the start.
        for (int i = chars.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars.ToArray());
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
}