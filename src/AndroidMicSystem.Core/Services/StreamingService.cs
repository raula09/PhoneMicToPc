using AndroidMicSystem.Core.Audio;
using AndroidMicSystem.Core.Models;
using AndroidMicSystem.Core.Network;

namespace AndroidMicSystem.Core.Services;

public class StreamingService : IDisposable
{
    private readonly UdpAudioStreamer _audioStreamer;
    private readonly TcpControlChannel _controlChannel;
    private readonly DeviceDiscovery _discovery;
    private readonly AudioEncoder _encoder;
    private readonly AudioDecoder _decoder;
    
    public ConnectionState ConnectionState { get; private set; } = new();
    public AudioSettings AudioSettings { get; private set; } = new();
    
    public event Action<ConnectionState>? ConnectionStateChanged;
    public event Action<byte[]>? AudioDataReceived;
    public event Action<Exception>? Error;
    
    public StreamingService(
        int audioPort = NetworkProtocol.DefaultAudioPort,
        int controlPort = NetworkProtocol.DefaultControlPort,
        int discoveryPort = NetworkProtocol.DefaultDiscoveryPort)
    {
        _audioStreamer = new UdpAudioStreamer(audioPort);
        _controlChannel = new TcpControlChannel(controlPort);
        _discovery = new DeviceDiscovery(discoveryPort);
        _encoder = new AudioEncoder(AudioSettings);
        _decoder = new AudioDecoder();
        
        SetupEventHandlers();
    }
    
    private void SetupEventHandlers()
    {
        _audioStreamer.PacketReceived += OnAudioPacketReceived;
        _audioStreamer.Error += OnError;
        
        _controlChannel.MessageReceived += OnControlMessageReceived;
        _controlChannel.ClientConnected += OnClientConnected;
        _controlChannel.ClientDisconnected += OnClientDisconnected;
        _controlChannel.Error += OnError;
        
        _discovery.Error += OnError;
    }
    
    public void StartServer()
    {
        try
        {
            _audioStreamer.StartReceiving();
            _controlChannel.StartListening();
            _discovery.StartListening();
            
            UpdateConnectionState(ConnectionStatus.Connecting, "Waiting for connection...");
        }
        catch (Exception ex)
        {
            UpdateConnectionState(ConnectionStatus.Error, $"Failed to start server: {ex.Message}");
            OnError(ex);
        }
    }
    
    public async Task<bool> ConnectToServerAsync(string host, int controlPort)
    {
        try
        {
            UpdateConnectionState(ConnectionStatus.Connecting, $"Connecting to {host}...");
            
            bool connected = await _controlChannel.ConnectAsync(host, controlPort);
            
            if (connected)
            {
                UpdateConnectionState(ConnectionStatus.Connected, $"Connected to {host}");
                return true;
            }
            else
            {
                UpdateConnectionState(ConnectionStatus.Error, "Connection failed");
                return false;
            }
        }
        catch (Exception ex)
        {
            UpdateConnectionState(ConnectionStatus.Error, $"Connection error: {ex.Message}");
            OnError(ex);
            return false;
        }
    }
    
    public async Task StartStreamingAsync(string destinationHost, int audioPort)
    {
        try
        {
            await _controlChannel.SendMessageAsync(new NetworkProtocol.ControlMessage
            {
                Type = NetworkProtocol.ControlMessageType.StartStream,
                Payload = $"{audioPort}"
            });
            
            ConnectionState.RemoteAddress = destinationHost;
            ConnectionState.RemotePort = audioPort;
            UpdateConnectionState(ConnectionStatus.Streaming, "Streaming audio...");
        }
        catch (Exception ex)
        {
            UpdateConnectionState(ConnectionStatus.Error, $"Failed to start streaming: {ex.Message}");
            OnError(ex);
        }
    }
    
    public async Task StopStreamingAsync()
    {
        try
        {
            await _controlChannel.SendMessageAsync(new NetworkProtocol.ControlMessage
            {
                Type = NetworkProtocol.ControlMessageType.StopStream
            });
            
            UpdateConnectionState(ConnectionStatus.Connected, "Streaming stopped");
        }
        catch (Exception ex)
        {
            OnError(ex);
        }
    }
    
    public async Task SendAudioAsync(byte[] audioData)
    {
        if (ConnectionState.Status != ConnectionStatus.Streaming || 
            ConnectionState.RemoteAddress == null)
            return;
            
        try
        {
            var endpoint = new System.Net.IPEndPoint(
                System.Net.IPAddress.Parse(ConnectionState.RemoteAddress),
                ConnectionState.RemotePort);
            
            await _audioStreamer.SendAudioAsync(audioData, _encoder, endpoint);
            
            ConnectionState.BytesSent += audioData.Length;
            ConnectionState.PacketsSent++;
        }
        catch (Exception ex)
        {
            OnError(ex);
        }
    }
    
    public async Task BroadcastPresenceAsync(DeviceInfo deviceInfo)
    {
        await _discovery.BroadcastPresenceAsync(deviceInfo);
    }
    
    public IReadOnlyDictionary<string, DeviceInfo> GetDiscoveredDevices()
    {
        return _discovery.DiscoveredDevices;
    }
    
    public void Disconnect()
    {
        _controlChannel.Disconnect();
        _audioStreamer.StopReceiving();
        _discovery.Stop();
        
        UpdateConnectionState(ConnectionStatus.Disconnected, "Disconnected");
    }
    
    private void OnAudioPacketReceived(AudioPacket packet)
    {
        _decoder.ReceivePacket(packet);
        
        var nextPacket = _decoder.GetNextPacket();
        if (nextPacket != null)
        {
            AudioDataReceived?.Invoke(nextPacket.AudioData);
            
            ConnectionState.BytesReceived += nextPacket.AudioData.Length;
            ConnectionState.PacketsReceived++;
            ConnectionState.PacketLossRate = _decoder.GetPacketLossRate();
        }
    }
     
    private void OnControlMessageReceived(NetworkProtocol.ControlMessage message)
    {
        switch (message.Type)
        {
            case NetworkProtocol.ControlMessageType.StartStream:
                UpdateConnectionState(ConnectionStatus.Streaming, "Receiving audio stream...");
                break;
                
            case NetworkProtocol.ControlMessageType.StopStream:
                UpdateConnectionState(ConnectionStatus.Connected, "Stream stopped");
                break;
                
            case NetworkProtocol.ControlMessageType.Ping:
             
                _controlChannel.SendMessageAsync(new NetworkProtocol.ControlMessage
                {
                    Type = NetworkProtocol.ControlMessageType.Pong
                }).Wait();
                break;
        }
    }
    
    private void OnClientConnected()
    {
        UpdateConnectionState(ConnectionStatus.Connected, "Client connected");
    }
    
    private void OnClientDisconnected()
    {
        UpdateConnectionState(ConnectionStatus.Disconnected, "Client disconnected");
    }
    
    private void OnError(Exception ex)
    {
        Error?.Invoke(ex);
    }
    
    private void UpdateConnectionState(ConnectionStatus status, string? message = null)
    {
        ConnectionState.Status = status;
        
        if (message != null)
        {
            if (status == ConnectionStatus.Error)
                ConnectionState.ErrorMessage = message;
        }
        
        if (status == ConnectionStatus.Connected && !ConnectionState.ConnectedAt.HasValue)
        {
            ConnectionState.ConnectedAt = DateTime.Now;
        }
        else if (status == ConnectionStatus.Disconnected)
        {
            ConnectionState.ConnectedAt = null;
            ConnectionState.BytesSent = 0;
            ConnectionState.BytesReceived = 0;
            ConnectionState.PacketsSent = 0;
            ConnectionState.PacketsReceived = 0;
        }
        
        ConnectionStateChanged?.Invoke(ConnectionState);
    }
    
    public void Dispose()
    {
        Disconnect();
        _audioStreamer?.Dispose();
        _controlChannel?.Dispose();
        _discovery?.Dispose();
    }

}