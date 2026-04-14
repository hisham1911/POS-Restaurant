namespace KasserPro.Tests.Integration;

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Orders;
using KasserPro.Application.DTOs.Shifts;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;

public class DeleteModeConcurrentOrderPerformanceTests : IAsyncLifetime
{
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ITestOutputHelper _output;
    private readonly DeleteModeWebApplicationFactory _factory;

    public DeleteModeConcurrentOrderPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _factory = new DeleteModeWebApplicationFactory();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OpenShift_ThenCreate10ConcurrentOrders_InDeleteMode_ShouldAvoidBlocking()
    {
        var seedData = await SeedTestDataAsync();

        using var client = _factory.CreateClient();
        var token = TestHelpers.GenerateTestToken(
            userId: seedData.UserId,
            tenantId: seedData.TenantId,
            branchId: seedData.BranchId,
            role: "Cashier");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var openShiftResponse = await client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest
        {
            OpeningBalance = 500m
        });

        openShiftResponse.IsSuccessStatusCode.Should().BeTrue(because: "shift must be opened before creating orders");
        var openShiftResult = await DeserializeResponse<ShiftDto>(openShiftResponse);
        openShiftResult.Success.Should().BeTrue(because: $"open shift failed: {openShiftResult.Message}");

        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var orderTasks = Enumerable.Range(1, 10)
            .Select(index => CreateOrderAsync(client, seedData.ProductId, index, gate.Task))
            .ToArray();

        var totalStopwatch = Stopwatch.StartNew();
        gate.SetResult(true);
        var results = await Task.WhenAll(orderTasks);
        totalStopwatch.Stop();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode;";
            var journalMode = (await cmd.ExecuteScalarAsync() ?? string.Empty).ToString() ?? string.Empty;
            _output.WriteLine($"journal_mode={journalMode}");
        }

        var successCount = results.Count(r => r.Success);
        var failedCount = results.Length - successCount;
        var lockedCount = results.Count(r => r.IsLockingFailure);

        var minMs = results.Min(r => r.Duration.TotalMilliseconds);
        var avgMs = results.Average(r => r.Duration.TotalMilliseconds);
        var maxMs = results.Max(r => r.Duration.TotalMilliseconds);

        _output.WriteLine($"total_duration_ms={totalStopwatch.Elapsed.TotalMilliseconds:F1}");
        _output.WriteLine($"per_order_duration_ms=min:{minMs:F1},avg:{avgMs:F1},max:{maxMs:F1}");
        _output.WriteLine($"success={successCount},failed={failedCount},locking_failures={lockedCount}");

        var errorSummary = string.Join(" | ", results
            .Where(r => !r.Success)
            .Select(r => $"status={r.StatusCode},msg={r.Message}"));

        lockedCount.Should().Be(0, because: $"DELETE mode should not produce sqlite lock errors. {errorSummary}");
        successCount.Should().Be(10, because: $"all concurrent order requests should succeed. {errorSummary}");
    }

    private async Task<SeedData> SeedTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant
        {
            Name = "Delete Mode Perf Tenant",
            Slug = "delete-mode-" + Guid.NewGuid().ToString("N")[..8],
            TaxRate = 14m,
            IsTaxEnabled = true,
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Performance Branch",
            Code = "PERF-" + Guid.NewGuid().ToString("N")[..8],
            Address = "Cairo",
            Phone = "01000000000",
            DefaultTaxRate = 14m,
            DefaultTaxInclusive = false,
            CurrencyCode = "EGP",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var user = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "Perf Cashier",
            Email = "perf-" + Guid.NewGuid().ToString("N")[..8] + "@kasserpro.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Cashier,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var category = new Category
        {
            TenantId = tenant.Id,
            Name = "Performance Category",
            NameEn = "Performance Category",
            IsActive = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            TenantId = tenant.Id,
            CategoryId = category.Id,
            Name = "Performance Product",
            NameEn = "Performance Product",
            Sku = "PERF-" + Guid.NewGuid().ToString("N")[..8],
            Price = 100m,
            Cost = 70m,
            TaxRate = 14m,
            TaxInclusive = false,
            IsActive = true,
            TrackInventory = false
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return new SeedData(tenant.Id, branch.Id, user.Id, product.Id);
    }

    private async Task<OrderCallResult> CreateOrderAsync(HttpClient client, int productId, int requestNumber, Task gate)
    {
        await gate;

        var request = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            CustomerName = $"Load Test {requestNumber}",
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = productId, Quantity = 1 }
            }
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await client.PostAsJsonAsync("/api/orders", request);
            var body = await response.Content.ReadAsStringAsync();
            stopwatch.Stop();

            var isLockingFailure = body.Contains("database is locked", StringComparison.OrdinalIgnoreCase);
            var apiResult = JsonSerializer.Deserialize<ApiResponse<OrderDto>>(body, _jsonOptions);
            var isSuccess = response.IsSuccessStatusCode && apiResult?.Success == true;
            var message = apiResult?.Message ?? body;

            return new OrderCallResult(
                Success: isSuccess,
                IsLockingFailure: isLockingFailure,
                Duration: stopwatch.Elapsed,
                StatusCode: (int)response.StatusCode,
                Message: message);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var isLockingFailure = ex.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase);

            return new OrderCallResult(
                Success: false,
                IsLockingFailure: isLockingFailure,
                Duration: stopwatch.Elapsed,
                StatusCode: 0,
                Message: ex.Message);
        }
    }

    private async Task<ApiResponse<T>> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(content, _jsonOptions)
               ?? new ApiResponse<T> { Success = false, Message = "Failed to deserialize response" };
    }

    private sealed record SeedData(int TenantId, int BranchId, int UserId, int ProductId);

    private sealed record OrderCallResult(
        bool Success,
        bool IsLockingFailure,
        TimeSpan Duration,
        int StatusCode,
        string Message);

    private sealed class DeleteModeWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"kasserpro-delete-mode-{Guid.NewGuid():N}.db");

        private string ConnectionString => $"Data Source={_databasePath};Cache=Shared;Pooling=True";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "YourSuperSecretKeyHere_MustBe32Characters!",
                    ["Jwt:Issuer"] = "KasserPro",
                    ["Jwt:Audience"] = "KasserPro",
                    ["Jwt:ExpiryInHours"] = "24",
                    ["ConnectionStrings:DefaultConnection"] = ConnectionString
                });
            });

            builder.ConfigureServices(services =>
            {
                var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (dbContextDescriptor != null)
                {
                    services.Remove(dbContextDescriptor);
                }

                var dbContextOptionsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions));
                if (dbContextOptionsDescriptor != null)
                {
                    services.Remove(dbContextOptionsDescriptor);
                }

                services.RemoveAll(typeof(AppDbContext));
                services.RemoveAll(typeof(AuditSaveChangesInterceptor));
                services.AddSingleton<AuditSaveChangesInterceptor>();

                services.AddDbContext<AppDbContext>((sp, options) =>
                {
                    options.UseSqlite(ConnectionString);
                    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
                });

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();

                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA journal_mode=DELETE;";
                var journalMode = (command.ExecuteScalar() ?? string.Empty).ToString() ?? string.Empty;
                if (!string.Equals(journalMode, "delete", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Expected journal_mode=DELETE but got '{journalMode}'.");
                }
            });

            builder.UseEnvironment("Testing");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            try
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }
            }
            catch
            {
                // Best effort cleanup for temp test database.
            }
        }
    }
}
