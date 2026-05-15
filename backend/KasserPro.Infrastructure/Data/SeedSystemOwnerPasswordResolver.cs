namespace KasserPro.Infrastructure.Data;

public enum SeedSystemOwnerPasswordSource
{
    Environment,
    FixedDefault
}

public static class SeedSystemOwnerPasswordResolver
{
    public const string EnvironmentVariableName = "KASSERPRO_SEED_SYSTEM_OWNER_PASSWORD";
    public const string FixedDefaultPassword = "Owner@123";

    public static string Resolve(out bool fromEnvironment)
    {
        var password = ResolveWithSource(out var source);
        fromEnvironment = source == SeedSystemOwnerPasswordSource.Environment;
        return password;
    }

    public static string ResolveWithSource(out SeedSystemOwnerPasswordSource source)
    {
        var envPassword = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(envPassword))
        {
            source = SeedSystemOwnerPasswordSource.Environment;
            return envPassword.Trim();
        }

        source = SeedSystemOwnerPasswordSource.FixedDefault;
        return FixedDefaultPassword;
    }
}
