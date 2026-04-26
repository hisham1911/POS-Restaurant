namespace KasserPro.Tests;

using KasserPro.Application.Common.Interfaces;

public class TestCurrentUserService : ICurrentUserService
{
    public int UserId { get; init; }
    public int TenantId { get; init; }
    public int BranchId { get; init; }
    public string? Email { get; init; }
    public string? Role { get; init; }
    public bool IsAuthenticated { get; init; } = true;
}
