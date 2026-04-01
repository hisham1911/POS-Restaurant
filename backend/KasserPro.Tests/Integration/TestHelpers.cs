namespace KasserPro.Tests.Integration;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Helper methods for integration tests
/// </summary>
public static class TestHelpers
{
    // Must match appsettings.json
    private const string JwtKey = "YourSuperSecretKeyHere_MustBe32Characters!";
    private const string JwtIssuer = "KasserPro";
    private const string JwtAudience = "KasserPro";

    /// <summary>
    /// Generate a JWT token for testing with specified claims
    /// </summary>
    public static string GenerateTestToken(
        int userId,
        int tenantId,
        int branchId,
        string email = "test@test.com",
        string name = "Test User",
        string role = "Cashier",
        IEnumerable<string>? permissions = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var claims = new List<Claim>
        {
            new("userId", userId.ToString()),
            new("tenantId", tenantId.ToString()),
            new("branchId", branchId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };

        foreach (var permission in permissions ?? new[] { "OrdersView", "OrdersCreate" })
        {
            claims.Add(new Claim("permission", permission));
        }

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
