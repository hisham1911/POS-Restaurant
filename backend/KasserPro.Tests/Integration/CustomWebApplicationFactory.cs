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

    public CustomWebApplicationFactory(
        bool includeJwtExpiry = true,
        bool includeJwtIssuerAudience = true)
    {
        _includeJwtExpiry = includeJwtExpiry;
        _includeJwtIssuerAudience = includeJwtIssuerAudience;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var configuration = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "YourSuperSecretKeyHere_MustBe32Characters!"
            };

            if (_includeJwtIssuerAudience)
            {
                configuration["Jwt:Issuer"] = "KasserPro";
                configuration["Jwt:Audience"] = "KasserPro";
            }
            else
            {
                configuration["Jwt:Issuer"] = null;
                configuration["Jwt:Audience"] = null;
            }

            if (_includeJwtExpiry)
            {
                configuration["Jwt:ExpiryInHours"] = "24";
            }
            else
            {
                configuration["Jwt:ExpiryInHours"] = null;
            }

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
        }
    }
}
