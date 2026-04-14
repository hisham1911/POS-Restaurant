namespace KasserPro.API.Controllers;

using KasserPro.API.Hubs;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/printer-status")]
[Authorize]
public class PrinterStatusController : ControllerBase
{
    private readonly ILogger<PrinterStatusController> _logger;

    public PrinterStatusController(ILogger<PrinterStatusController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetPrinterStatus()
    {
        try
        {
            var branchId = User.FindFirst("branchId")?.Value ?? "default";
            var primaryGroup = $"branch-{branchId}";
            const string fallbackGroup = "branch-default";

            var scopedDevices = DeviceHub.GetConnectedDevicesForGroups(primaryGroup, fallbackGroup);
            var preferredConnectionId = DeviceHub.GetPreferredConnectionId(primaryGroup)
                ?? (string.Equals(primaryGroup, fallbackGroup, StringComparison.Ordinal)
                    ? null
                    : DeviceHub.GetPreferredConnectionId(fallbackGroup));

            var preferredDevice = scopedDevices.FirstOrDefault(device =>
                string.Equals(device.ConnectionId, preferredConnectionId, StringComparison.Ordinal));

            var response = new PrinterStatusDto
            {
                PrimaryGroup = primaryGroup,
                FallbackGroup = fallbackGroup,
                BridgeAvailable = scopedDevices.Count > 0,
                TotalDevicesInScope = scopedDevices.Count,
                PreferredDeviceConnectionId = preferredConnectionId,
                PreferredDevice = preferredDevice == null ? null : MapDevice(preferredDevice),
                Devices = scopedDevices.Select(MapDevice).ToList(),
                CheckedAtUtc = DateTime.UtcNow,
            };

            return Ok(ApiResponse<PrinterStatusDto>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get printer bridge status");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail(ErrorCodes.INTERNAL_ERROR, ErrorMessages.Get(ErrorCodes.INTERNAL_ERROR)));
        }
    }

    private static ConnectedPrinterDeviceDto MapDevice(DeviceHub.ConnectedDeviceSnapshot snapshot)
    {
        var displayName = !string.IsNullOrWhiteSpace(snapshot.DeviceName)
            ? snapshot.DeviceName!
            : !string.IsNullOrWhiteSpace(snapshot.MachineName)
                ? snapshot.MachineName!
                : snapshot.DeviceId;

        return new ConnectedPrinterDeviceDto
        {
            ConnectionId = snapshot.ConnectionId,
            DeviceId = snapshot.DeviceId,
            DeviceName = displayName,
            MachineName = snapshot.MachineName,
            PrinterName = snapshot.PrinterName,
            GroupName = snapshot.GroupName,
            ConnectedAtUtc = snapshot.ConnectedAtUtc,
        };
    }
}

public class PrinterStatusDto
{
    public string PrimaryGroup { get; set; } = string.Empty;
    public string FallbackGroup { get; set; } = "branch-default";
    public bool BridgeAvailable { get; set; }
    public int TotalDevicesInScope { get; set; }
    public string? PreferredDeviceConnectionId { get; set; }
    public ConnectedPrinterDeviceDto? PreferredDevice { get; set; }
    public List<ConnectedPrinterDeviceDto> Devices { get; set; } = new();
    public DateTime CheckedAtUtc { get; set; }
}

public class ConnectedPrinterDeviceDto
{
    public string ConnectionId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? MachineName { get; set; }
    public string? PrinterName { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime ConnectedAtUtc { get; set; }
}
