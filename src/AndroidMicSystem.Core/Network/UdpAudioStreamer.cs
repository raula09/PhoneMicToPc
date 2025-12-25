using System.Net;
using System.Net.Sockets;
using AndroidMicSystem.Core.Audio;

namespace AndroidMicSystem.Core.Network;

public class UdpAudioStreamer : IDisposable
{
    private UdpClient? _udpClient;
    private readonly int _port;
    private bool _isReceiving;
    private CancellationTokenSource? _receiveCts;
    
    public event Action<AudioPacket>? PacketReceived;
    public event Action<Exception>? Error;
    
    public UdpAudioStreamer(int port = 5000)
    {
        _port = port;
    }
    
    public void StartReceiving()
    {
        if (_isReceiving)
            return;
            
        _isReceiving = true;
        _receiveCts = new CancellationTokenSource();
        
        try
        {
            if (_udpClient != null)
            {
                try { _udpClient.Close(); } catch { }
                try { _udpClient.Dispose(); } catch { }
            }
            
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.ReceiveBufferSize = 1024 * 1024;
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
        Thread.Sleep(100); 
    }
    
    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        while (_isReceiving && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_udpClient == null)
                    break;
                    
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
