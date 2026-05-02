namespace KasserPro.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KasserPro.Application.DTOs.Auth;
using KasserPro.Application.DTOs.Common;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class AuthIntegrationTests
{
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Login_WhenJwtExpiryIsMissing_ReturnsTokenWithDefaultExpiry()
    {
        using var factory = new CustomWebApplicationFactory(includeJwtExpiry: false, includeJwtIssuerAudience: true);
        var email = await SeedLoginUserAsync(factory);
        using var client = factory.CreateClient();
        var beforeLogin = DateTime.UtcNow;

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Test123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(content, _jsonOptions);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Data.ExpiresAt.Should().BeAfter(beforeLogin.AddHours(23));
        result.Data.ExpiresAt.Should().BeBefore(beforeLogin.AddHours(25));
    }

    [Fact]
    public async Task Login_WhenJwtIssuerAndAudienceAreMissing_TokenCanAccessProtectedEndpoint()
    {
        using var factory = new CustomWebApplicationFactory(includeJwtExpiry: true, includeJwtIssuerAudience: false);
        var email = await SeedLoginUserAsync(factory);
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Test123!"
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(loginContent, _jsonOptions);
        var token = loginResult!.Data!.AccessToken;

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var meResponse = await client.GetAsync("/api/auth/me");

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<string> SeedLoginUserAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant
        {
            Name = "Auth Tenant",
            Slug = "auth-" + Guid.NewGuid().ToString()[..8],
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "Auth Branch",
            Code = "AUTH-" + Guid.NewGuid().ToString()[..6],
            DefaultTaxRate = 14m,
            CurrencyCode = "EGP",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var email = "auth-" + Guid.NewGuid().ToString()[..8] + "@test.com";
        db.Users.Add(new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "Auth Admin",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        });
        await db.SaveChangesAsync();

        return email;
    }
}
