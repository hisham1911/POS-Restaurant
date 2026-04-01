namespace KasserPro.Application.DTOs.System;

public class SystemInfoDto
{
    public string LanIp { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsOffline { get; set; }
}
