using Microsoft.AspNetCore.SignalR;

namespace KasserPro.API.Hubs;

/// <summary>
/// SignalR hub for communication with Desktop Bridge App devices
/// </summary>
public class DeviceHub : Hub
{
    private readonly ILogger<DeviceHub> _logger;
    private static readonly object _connectionLock = new();
    private static readonly Dictionary<string, string> _deviceConnections = new();
    private static readonly Dictionary<string, DeviceConnectionInfo> _connectionInfoByConnectionId = new();

    private sealed record DeviceConnectionInfo(
        string DeviceId,
        string GroupName,
        DateTime ConnectedAtUtc,
        string? DeviceName,
        string? PrinterName,
        string? MachineName);

    public sealed record ConnectedDeviceSnapshot(
        string ConnectionId,
        string DeviceId,
        string GroupName,
        DateTime ConnectedAtUtc,
        string? DeviceName,
        string? PrinterName,
        string? MachineName);

    public DeviceHub(ILogger<DeviceHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a device connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext == null)
        {
            _logger.LogWarning("No HTTP context available");
            Context.Abort();
            return;
        }

        var deviceId = httpContext.Request.Headers["X-Device-Id"].ToString();
        var apiKey = httpContext.Request.Headers["X-API-Key"].ToString();
        var deviceName = NormalizeHeaderValue(httpContext.Request.Headers["X-Device-Name"].ToString());
        var printerName = NormalizeHeaderValue(httpContext.Request.Headers["X-Printer-Name"].ToString());
        var machineName = NormalizeHeaderValue(httpContext.Request.Headers["X-Machine-Name"].ToString());

        // Validate API key (simplified for MVP - in production, validate against database)
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Device connection rejected: No API key provided");
            Context.Abort();
            return;
        }

        if (string.IsNullOrEmpty(deviceId))
        {
            _logger.LogWarning("Device connection rejected: No Device ID provided");
            Context.Abort();
            return;
        }

        // P0-5: Add device to a branch group for targeted receipt delivery.
        // Branch ID comes from the X-Branch-Id header (set by desktop bridge config).
        // Default to "branch-default" if not provided.
        var branchId = httpContext.Request.Headers["X-Branch-Id"].ToString();
        var groupName = !string.IsNullOrEmpty(branchId) ? $"branch-{branchId}" : "branch-default";

        // Store device connection and group metadata for deterministic single-device routing.
        lock (_connectionLock)
        {
            if (_deviceConnections.TryGetValue(deviceId, out var previousConnectionId)
                && !string.Equals(previousConnectionId, Context.ConnectionId, StringComparison.Ordinal))
            {
                _connectionInfoByConnectionId.Remove(previousConnectionId);
            }

            _deviceConnections[deviceId] = Context.ConnectionId;
            _connectionInfoByConnectionId[Context.ConnectionId] =
                new DeviceConnectionInfo(
                    deviceId,
                    groupName,
                    DateTime.UtcNow,
                    deviceName,
                    printerName,
                    machineName);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Device {DeviceId} connected with connection ID {ConnectionId} and added to group {GroupName}. DeviceName={DeviceName}, PrinterName={PrinterName}",
            deviceId,
            Context.ConnectionId,
            groupName,
            deviceName ?? "Unknown",
            printerName ?? "Unknown");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a device disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? disconnectedDeviceId = null;

        lock (_connectionLock)
        {
            if (_connectionInfoByConnectionId.TryGetValue(Context.ConnectionId, out var info))
            {
                disconnectedDeviceId = info.DeviceId;
                _connectionInfoByConnectionId.Remove(Context.ConnectionId);

                if (_deviceConnections.TryGetValue(info.DeviceId, out var activeConnectionId)
                    && string.Equals(activeConnectionId, Context.ConnectionId, StringComparison.Ordinal))
                {
                    _deviceConnections.Remove(info.DeviceId);
                }
            }
        }

        if (!string.IsNullOrEmpty(disconnectedDeviceId))
        {
            _logger.LogInformation("Device {DeviceId} disconnected", disconnectedDeviceId);
        }

        if (exception != null)
        {
            _logger.LogError(exception, "Device disconnected with error");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by Desktop App to report print completion
    /// </summary>
    /// <param name="eventDto">Print completion event data</param>
    public async Task PrintCompleted(PrintCompletedEventDto eventDto)
    {
        _logger.LogInformation(
            "Print completed: CommandId={CommandId}, Success={Success}, Error={Error}",
            eventDto.CommandId,
            eventDto.Success,
            eventDto.ErrorMessage ?? "None"
        );

        // P0-5: Notify the caller that print is complete (no need to broadcast status)
        await Clients.Caller.SendAsync("PrintCompleted", eventDto);
    }

    /// <summary>
    /// Gets count of currently connected devices
    /// </summary>
    public static int GetConnectedDeviceCount()
    {
        lock (_connectionLock)
        {
            return _deviceConnections.Count;
        }
    }

    /// <summary>
    /// Returns a preferred single device connection for a group.
    /// Uses oldest active connection to keep routing deterministic.
    /// </summary>
    public static string? GetPreferredConnectionId(string groupName)
    {
        lock (_connectionLock)
        {
            return _connectionInfoByConnectionId
                .Where(pair => string.Equals(pair.Value.GroupName, groupName, StringComparison.Ordinal))
                .OrderBy(pair => pair.Value.ConnectedAtUtc)
                .Select(pair => pair.Key)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Returns connected device snapshots for one or more SignalR groups.
    /// </summary>
    public static IReadOnlyList<ConnectedDeviceSnapshot> GetConnectedDevicesForGroups(params string[] groupNames)
    {
        if (groupNames == null || groupNames.Length == 0)
        {
            return Array.Empty<ConnectedDeviceSnapshot>();
        }

        var allowedGroups = new HashSet<string>(
            groupNames.Where(groupName => !string.IsNullOrWhiteSpace(groupName)),
            StringComparer.Ordinal);

        if (allowedGroups.Count == 0)
        {
            return Array.Empty<ConnectedDeviceSnapshot>();
        }

        lock (_connectionLock)
        {
            return _connectionInfoByConnectionId
                .Where(pair => allowedGroups.Contains(pair.Value.GroupName))
                .OrderBy(pair => pair.Value.ConnectedAtUtc)
                .Select(pair => new ConnectedDeviceSnapshot(
                    pair.Key,
                    pair.Value.DeviceId,
                    pair.Value.GroupName,
                    pair.Value.ConnectedAtUtc,
                    pair.Value.DeviceName,
                    pair.Value.PrinterName,
                    pair.Value.MachineName))
                .ToList();
        }
    }

    private static string? NormalizeHeaderValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}

/// <summary>
/// Print completion event DTO
/// </summary>
public class PrintCompletedEventDto
{
    public string CommandId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CompletedAt { get; set; }
}
