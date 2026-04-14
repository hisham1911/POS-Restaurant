namespace KasserPro.Infrastructure.Data;

public static class SeedTenantRegistry
{
    public static readonly string[] Slugs =
    {
        "al-amana-butcher",
        "supermarket"
    };

    private static readonly HashSet<string> SlugSet = new(Slugs, StringComparer.OrdinalIgnoreCase);

    public static bool Contains(string? slug)
    {
        return !string.IsNullOrWhiteSpace(slug) && SlugSet.Contains(slug);
    }
}
