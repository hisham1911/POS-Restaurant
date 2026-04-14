namespace KasserPro.Infrastructure.Data;

using System.Security.Cryptography;

public static class SeedSystemOwnerPasswordResolver
{
    public const string EnvironmentVariableName = "KASSERPRO_SEED_SYSTEM_OWNER_PASSWORD";

    private const int GeneratedPasswordLength = 24;
    private static string? _cachedGeneratedPassword;

    public static string Resolve(out bool fromEnvironment)
    {
        var envPassword = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(envPassword))
        {
            fromEnvironment = true;
            return envPassword.Trim();
        }

        fromEnvironment = false;
        _cachedGeneratedPassword ??= GeneratePassword(GeneratedPasswordLength);
        return _cachedGeneratedPassword;
    }

    private static string GeneratePassword(int length)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%*-_";
        var output = new char[length];
        var buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);

        for (var i = 0; i < length; i++)
        {
            output[i] = alphabet[buffer[i] % alphabet.Length];
        }

        return new string(output);
    }
}
