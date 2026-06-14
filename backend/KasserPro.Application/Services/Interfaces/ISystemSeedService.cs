namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;

public interface ISystemSeedService
{
    Task EnsureSystemOwnerAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<SystemSeedRunResultDto>> RunFullSeedPipelineAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<SystemSeedRunResultDto>> SeedRestaurantDemoAsync(CancellationToken cancellationToken = default);
}

public class SystemSeedRunResultDto
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public long DurationMs { get; set; }
    public bool InventorySynchronizationTriggered { get; set; }
    public bool PreservedExistingData { get; set; } = true;
    public List<string> SeededTenantSlugs { get; set; } = new();
    public List<string> OptionalWarnings { get; set; } = new();
}
