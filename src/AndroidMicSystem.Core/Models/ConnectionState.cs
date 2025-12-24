namespace AndroidMicSystem.Core.Models;


public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Streaming,
    Error
}

public class ConnectionState
{
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Disconnected;
    public string? RemoteAddress { get; set; }
    public int RemotePort { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public string? ErrorMessage { get; set; }
    
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public int PacketsSent { get; set; }
    public int PacketsReceived { get; set; }
    public double PacketLossRate { get; set; }
    
    public bool IsConnected => Status == ConnectionStatus.Connected || Status == ConnectionStatus.Streaming;
    
    public TimeSpan? ConnectionDuration => ConnectedAt.HasValue 
        ? DateTime.Now - ConnectedAt.Value 
        : null;
}


public class DeviceInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.Now;
}