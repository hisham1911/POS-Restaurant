namespace KasserPro.Tests.Integration;

using FluentAssertions;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using KasserPro.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class SystemSeedServiceTests
{
    [Fact]
    public async Task EnsureSystemOwnerAsync_OnFreshDatabaseWithAuditInterceptor_CreatesOwner()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var auditInterceptor = new AuditSaveChangesInterceptor(new HttpContextAccessor());
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(auditInterceptor)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new SystemSeedService(context, NullLogger<SystemSeedService>.Instance);

        var act = async () => await service.EnsureSystemOwnerAsync();

        await act.Should().NotThrowAsync();

        var owner = await context.Users.SingleAsync(user => user.Email == "owner@kasserpro.com");
        owner.Role.Should().Be(UserRole.SystemOwner);
        owner.TenantId.Should().BeNull();
        owner.BranchId.Should().BeNull();
        owner.IsActive.Should().BeTrue();

        var auditLogCount = await context.AuditLogs.CountAsync();
        auditLogCount.Should().Be(0);
    }
}
