using System.Net;
using System.Net.Sockets;
using AndroidMicSystem.Core.Audio;

namespace AndroidMicSystem.Core.Network;


public class UdpAudioStreamer : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly int _port;
    private bool _isReceiving;
    private CancellationTokenSource? _receiveCts;
    
    public event Action<AudioPacket>? PacketReceived;
    public event Action<Exception>? Error;
    
    public UdpAudioStreamer(int port = NetworkProtocol.DefaultAudioPort)
    {
        _port = port;
        _udpClient = new UdpClient();
        _udpClient.Client.ReceiveBufferSize = 1024 * 1024; 
    }
    
   
    public void StartReceiving()
    {
        if (_isReceiving)
            return;
            
        _isReceiving = true;
        _receiveCts = new CancellationTokenSource();
        
        try
        {
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            Task.Run(() => ReceiveLoop(_receiveCts.Token));
        }
        catch (Exception ex)
        {
            _isReceiving = false;
            Error?.Invoke(ex);
        }
    }
    
  
    public void StopReceiving()
    {
        _isReceiving = false;
        _receiveCts?.Cancel();
    }
    
  
    public async Task SendPacketAsync(AudioPacket packet, IPEndPoint remoteEndpoint)
    {
        try
        {
            byte[] data = packet.ToBytes();
            await _udpClient.SendAsync(data, data.Length, remoteEndpoint);
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
    }
    
    public async Task SendAudioAsync(byte[] audioData, AudioEncoder encoder, IPEndPoint remoteEndpoint)
    {
        try
        {
            uint timestamp = (uint)DateTime.Now.Ticks;
            var packet = encoder.CreatePacket(audioData, timestamp);
            await SendPacketAsync(packet, remoteEndpoint);
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
    }
    
    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        while (_isReceiving && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(cancellationToken);
                var packet = AudioPacket.FromBytes(result.Buffer);
                
                if (packet != null)
                {
                    PacketReceived?.Invoke(packet);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_isReceiving)
                {
                    Error?.Invoke(ex);
                }
            }
        }
    }
    
    public void Dispose()
    {
        StopReceiving();
        _udpClient?.Dispose();
        _receiveCts?.Dispose();
    }
}