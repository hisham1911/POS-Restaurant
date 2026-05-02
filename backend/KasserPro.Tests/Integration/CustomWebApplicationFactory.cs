namespace KasserPro.Tests.Integration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KasserPro.Infrastructure.Data;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Uses SQLite in-memory database to isolate tests from production data.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly bool _includeJwtExpiry;
    private readonly bool _includeJwtIssuerAudience;
    private SqliteConnection? _connection;

    public CustomWebApplicationFactory()
    {
        _includeJwtExpiry = true;
        _includeJwtIssuerAudience = true;
    }

    internal CustomWebApplicationFactory(
        bool includeJwtExpiry,
        bool includeJwtIssuerAudience)
    {
        _includeJwtExpiry = includeJwtExpiry;
        _includeJwtIssuerAudience = includeJwtIssuerAudience;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("KASSERPRO_DB_PASSWORD", "TestDummyPassword!");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var configuration = new Dictionary<string, string?>();

            // Note: Jwt:Key is NOT overridden here because Program.cs reads builder.Configuration
            // before WebApplicationFactory.ConfigureAppConfiguration is applied. We rely on the
            // Jwt__Key environment variable (same source Program.cs falls back to).
            // This ensures AuthService (which sees the DI IConfiguration) and Program.cs use the same key.

            if (_includeJwtIssuerAudience)
            {
                configuration["Jwt:Issuer"] = "KasserPro";
                configuration["Jwt:Audience"] = "KasserPro";
            }
            // When _includeJwtIssuerAudience is false, we intentionally do NOT add the keys at all.
            // Setting them to null produces empty-string in MemoryConfigurationProvider, which
            // breaks the ?? fallback in AuthService vs the default fallback in Program.cs.

            if (_includeJwtExpiry)
            {
                configuration["Jwt:ExpiryInHours"] = "24";
            }
            // When expiry is disabled, we omit the key so AuthService falls back to default (24).
            // Program.cs never reads this value.

            configBuilder.AddInMemoryCollection(configuration);
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext-related registrations
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor != null)
                services.Remove(dbContextDescriptor);

            // Remove the generic DbContextOptions
            var dbContextOptionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions));
            if (dbContextOptionsDescriptor != null)
                services.Remove(dbContextOptionsDescriptor);

            // Remove the AppDbContext itself
            services.RemoveAll(typeof(AppDbContext));

            // Remove the original AuditSaveChangesInterceptor and re-add it
            // This ensures it's available for the test DbContext configuration
            services.RemoveAll(typeof(AuditSaveChangesInterceptor));
            services.AddSingleton<AuditSaveChangesInterceptor>();

            // Create and open a SQLite in-memory connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite in-memory database for testing
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseSqlite(_connection);
                // Add the interceptor for tests too (it's safe, just won't have HttpContext)
                options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
            });

            // Build service provider and ensure database is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
            Environment.SetEnvironmentVariable("KASSERPRO_DB_PASSWORD", null);
        }
    }
}
