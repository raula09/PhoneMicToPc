using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using AndroidMicSystem.Core.Models;
using static AndroidMicSystem.Core.Network.NetworkProtocol;

namespace AndroidMicSystem.Core.Network;

/// <summary>
/// UDP-based device discovery for finding Android phones on the network
/// </summary>
public class DeviceDiscovery : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly int _port;
    private bool _isRunning;
    private CancellationTokenSource? _cts;
    private readonly Dictionary<string, DeviceInfo> _discoveredDevices = new();
    
    public event Action<DeviceInfo>? DeviceDiscovered;
    public event Action<string>? DeviceLost;
    public event Action<Exception>? Error;
    
    public IReadOnlyDictionary<string, DeviceInfo> DiscoveredDevices => _discoveredDevices;
    
    public DeviceDiscovery(int port = NetworkProtocol.DefaultDiscoveryPort)
    {
        _port = port;
        _udpClient = new UdpClient();
        _udpClient.EnableBroadcast = true;
    }
    
    public void StartListening()
    {
        if (_isRunning)
            return;
            
        _isRunning = true;
        _cts = new CancellationTokenSource();
        
        try
        {
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            Task.Run(() => ReceiveLoop(_cts.Token));
            Task.Run(() => CleanupLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            _isRunning = false;
            Error?.Invoke(ex);
        }
    }
    
    
    public async Task BroadcastPresenceAsync(DeviceInfo deviceInfo)
    {
        try
        {
            var message = new ControlMessage
            {
                Type = ControlMessageType.DiscoveryRequest,
                Payload = JsonSerializer.Serialize(deviceInfo)
            };
            
            byte[] data = message.ToBytes();
            var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, _port);
            
            await _udpClient.SendAsync(data, data.Length, broadcastEndpoint);
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
    }
    
    public async Task SendDiscoveryResponseAsync(DeviceInfo deviceInfo, IPEndPoint remoteEndpoint)
    {
        try
        {
            var message = new ControlMessage
            {
                Type = ControlMessageType.DiscoveryResponse,
                Payload = JsonSerializer.Serialize(deviceInfo)
            };
            
            byte[] data = message.ToBytes();
            await _udpClient.SendAsync(data, data.Length, remoteEndpoint);
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
    }
    
    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
    }
    
    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(cancellationToken);
                var message = ControlMessage.FromBytes(result.Buffer);
                
                if (message != null && 
                    (message.Type == ControlMessageType.DiscoveryRequest || 
                     message.Type == ControlMessageType.DiscoveryResponse))
                {
                    try
                    {
                        var deviceInfo = JsonSerializer.Deserialize<DeviceInfo>(message.Payload);
                        if (deviceInfo != null)
                        {
                            deviceInfo.LastSeen = DateTime.Now;
                            
                            if (!_discoveredDevices.ContainsKey(deviceInfo.DeviceId))
                            {
                                _discoveredDevices[deviceInfo.DeviceId] = deviceInfo;
                                DeviceDiscovered?.Invoke(deviceInfo);
                            }
                            else
                            {
                                _discoveredDevices[deviceInfo.DeviceId] = deviceInfo;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Error?.Invoke(ex);
                }
            }
        }
    }
    
    private async Task CleanupLoop(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, cancellationToken); 
                
                var timeout = TimeSpan.FromSeconds(30);
                var now = DateTime.Now;
                
                var lostDevices = _discoveredDevices
                    .Where(kvp => now - kvp.Value.LastSeen > timeout)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var deviceId in lostDevices)
                {
                    _discoveredDevices.Remove(deviceId);
                    DeviceLost?.Invoke(deviceId);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Error?.Invoke(ex);
                }
            }
        }
    }
    
    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
        _cts?.Dispose();
    }
}