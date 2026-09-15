namespace Gateway.Areas.Admin.Services;

public static class TemporaryPasswordGenerator
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";

    public static string Generate(int length = 12)
    {
        var random = new Random();
        var password = new char[length];
        for (int i = 0; i < password.Length; i++)
        {
            password[i] = Chars[random.Next(Chars.Length)];
        }
        return new string(password);
    }
}