using System.Net;
using System.Net.Sockets;
using static AndroidMicSystem.Core.Network.NetworkProtocol;

namespace AndroidMicSystem.Core.Network;

public class TcpControlChannel : IDisposable
{
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly int _port;
    private bool _isRunning;
    private CancellationTokenSource? _cts;
    
    public event Action<ControlMessage>? MessageReceived;
    public event Action? ClientConnected;
    public event Action? ClientDisconnected;
    public event Action<Exception>? Error;
    
    public bool IsConnected => _client?.Connected ?? false;
    
    public TcpControlChannel(int port = NetworkProtocol.DefaultControlPort)
    {
        _port = port;
    }
    
    public void StartListening()
    {
        if (_isRunning)
            return;
            
        _isRunning = true;
        _cts = new CancellationTokenSource();
        
        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            Task.Run(() => AcceptLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            _isRunning = false;
            Error?.Invoke(ex);
        }
    }
    
    public async Task<bool> ConnectAsync(string host, int port)
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            
            _cts = new CancellationTokenSource();
            Task.Run(() => ReceiveLoop(_cts.Token));
            
            ClientConnected?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
            return false;
        }
    }
    
    public async Task<bool> SendMessageAsync(ControlMessage message)
    {
        if (_stream == null || !IsConnected)
            return false;
            
        try
        {
            byte[] data = message.ToBytes();
            await _stream.WriteAsync(data, 0, data.Length);
            await _stream.FlushAsync();
            return true;
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
            return false;
        }
    }
    
    public void Disconnect()
    {
        _cts?.Cancel();
        _stream?.Close();
        _client?.Close();
        _stream = null;
        _client = null;
        ClientDisconnected?.Invoke();
    }
    
    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        Disconnect();
        _listener?.Stop();
        _listener = null;
    }
    
    private async Task AcceptLoop(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_listener == null)
                    break;
                    
                _client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _stream = _client.GetStream();
                
                ClientConnected?.Invoke();
                
                await ReceiveLoop(cancellationToken);
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
    
    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];
        
        while (IsConnected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_stream == null)
                    break;
                    
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                
                if (bytesRead == 0)
                {
                    Disconnect();
                    break;
                }
                
                byte[] messageData = new byte[bytesRead];
                Array.Copy(buffer, messageData, bytesRead);
                
                var message = ControlMessage.FromBytes(messageData);
                if (message != null)
                {
                    MessageReceived?.Invoke(message);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (IsConnected)
                {
                    Error?.Invoke(ex);
                }
                Disconnect();
                break;
            }
        }
    }
    
    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}