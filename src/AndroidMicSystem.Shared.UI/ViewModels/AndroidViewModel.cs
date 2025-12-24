using System;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Android.Content;
using AndroidMicSystem.Core.Services;
using AndroidMicSystem.Core.Models;
using AndroidMicSystem.Android.Services;

namespace AndroidMicSystem.Shared.UI.ViewModels;

public class AndroidViewModel : ReactiveObject
{
    private readonly StreamingService _streamingService;
    private string _serverAddress = "";
    private string _statusMessage = "Enter server address to connect";
    private bool _isStreaming;
    private string _connectionInfo = "";
    
    public AndroidViewModel()
    {
        _streamingService = new StreamingService();
        
        StartDiscoveryCommand = ReactiveCommand.CreateFromTask(StartDiscoveryAsync);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync,
            this.WhenAnyValue(x => x.ServerAddress, addr => !string.IsNullOrWhiteSpace(addr)));
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync);
        StartStreamingCommand = ReactiveCommand.Create(StartStreaming,
            this.WhenAnyValue(x => x.IsStreaming, streaming => !streaming));
        StopStreamingCommand = ReactiveCommand.Create(StopStreaming,
            this.WhenAnyValue(x => x.IsStreaming));
        
        _streamingService.ConnectionStateChanged += OnConnectionStateChanged;
        _streamingService.Error += OnError;
    }
    
    public string ServerAddress
    {
        get => _serverAddress;
        set => this.RaiseAndSetIfChanged(ref _serverAddress, value);
    }
    
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }
    
    public bool IsStreaming
    {
        get => _isStreaming;
        set => this.RaiseAndSetIfChanged(ref _isStreaming, value);
    }
    
    public string ConnectionInfo
    {
        get => _connectionInfo;
        set => this.RaiseAndSetIfChanged(ref _connectionInfo, value);
    }
    
    public ReactiveCommand<Unit, Unit> StartDiscoveryCommand { get; }
    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> StartStreamingCommand { get; }
    public ReactiveCommand<Unit, Unit> StopStreamingCommand { get; }
    
    private async Task StartDiscoveryAsync()
    {
        try
        {
            StatusMessage = "Broadcasting presence...";
            
            var deviceInfo = new DeviceInfo
            {
                DeviceId = global::Android.Provider.Settings.Secure.GetString(
                    global::Android.App.Application.Context.ContentResolver,
                    global::Android.Provider.Settings.Secure.AndroidId) ?? "unknown",
                DeviceName = $"{global::Android.OS.Build.Manufacturer} {global::Android.OS.Build.Model}",
                IpAddress = GetLocalIpAddress(),
                Port = 5001
            };
            
            await _streamingService.BroadcastPresenceAsync(deviceInfo);
            
            StatusMessage = "Presence broadcast sent";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    private async Task ConnectAsync()
    {
        try
        {
            StatusMessage = $"Connecting to {ServerAddress}...";
            
            bool connected = await _streamingService.ConnectToServerAsync(ServerAddress, 5001);
            
            if (connected)
            {
                StatusMessage = $"Connected to {ServerAddress}";
            }
            else
            {
                StatusMessage = "Connection failed";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    private async Task DisconnectAsync()
    {
        try
        {
            StopStreaming();
            _streamingService.Disconnect();
            
            StatusMessage = "Disconnected";
            IsStreaming = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        
        await Task.CompletedTask;
    }
    
    private void StartStreaming()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(StreamingForegroundService));
            intent.SetAction("START_STREAMING");
            intent.PutExtra("ServerAddress", ServerAddress);
            
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
            
            IsStreaming = true;
            StatusMessage = "Streaming started";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    private void StopStreaming()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(StreamingForegroundService));
            intent.SetAction("STOP_STREAMING");
            context.StartService(intent);
            
            IsStreaming = false;
            StatusMessage = "Streaming stopped";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    private void OnConnectionStateChanged(ConnectionState state)
    {
        ConnectionInfo = state.IsConnected
            ? $"Status: {state.Status}\n" +
              $"Packets sent: {state.PacketsSent}\n" +
              $"Data sent: {FormatBytes(state.BytesSent)}\n" +
              $"Duration: {state.ConnectionDuration?.ToString(@"hh\:mm\:ss")}"
            : "Not connected";
        
        if (!string.IsNullOrEmpty(state.ErrorMessage))
        {
            StatusMessage = state.ErrorMessage;
        }
    }
    
    private void OnError(Exception ex)
    {
        StatusMessage = $"Error: {ex.Message}";
    }
    
    private string GetLocalIpAddress()
    {
        try
        {
            var wifiManager = global::Android.App.Application.Context.GetSystemService(Context.WifiService) 
                as global::Android.Net.Wifi.WifiManager;
            
            if (wifiManager != null)
            {
                var wifiInfo = wifiManager.ConnectionInfo;
                int ipAddress = wifiInfo.IpAddress;
                
                return $"{ipAddress & 0xFF}.{(ipAddress >> 8) & 0xFF}.{(ipAddress >> 16) & 0xFF}.{(ipAddress >> 24) & 0xFF}";
            }
        }
        catch
        {
           
        }
        
        return "0.0.0.0";
    }
    
    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
}