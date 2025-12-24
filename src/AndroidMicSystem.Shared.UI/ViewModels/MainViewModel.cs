using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using AndroidMicSystem.Core.Models;
using AndroidMicSystem.Core.Services;
using AndroidMicSystem.Desktop.AudioInjection;

namespace AndroidMicSystem.Shared.UI.ViewModels;

public class MainViewModel : ReactiveObject
{
    private readonly StreamingService _streamingService;
    private readonly IPlatformAudioInjector _audioInjector;
    
    private string _statusMessage = "Waiting for connection...";
    private bool _isStreaming;
    private string _selectedDeviceId = string.Empty;
    private double _audioLevel;
    private string _connectionInfo = "";
    
    public MainViewModel()
    {
        _streamingService = new StreamingService();
        _audioInjector = new PipeWireAudioInjector();
        
        Devices = new ObservableCollection<DeviceInfo>();
        
        StartServerCommand = ReactiveCommand.CreateFromTask(StartServerAsync);
        StopServerCommand = ReactiveCommand.CreateFromTask(StopServerAsync);
        ConnectToDeviceCommand = ReactiveCommand.CreateFromTask(ConnectToDeviceAsync,
            this.WhenAnyValue(x => x.SelectedDeviceId, id => !string.IsNullOrEmpty(id)));
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync);
        RefreshDevicesCommand = ReactiveCommand.Create(RefreshDevices);
        
        _streamingService.ConnectionStateChanged += OnConnectionStateChanged;
        _streamingService.AudioDataReceived += OnAudioDataReceived;
        _streamingService.Error += OnError;
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
    
    public string SelectedDeviceId
    {
        get => _selectedDeviceId;
        set => this.RaiseAndSetIfChanged(ref _selectedDeviceId, value);
    }
    
    public double AudioLevel
    {
        get => _audioLevel;
        set => this.RaiseAndSetIfChanged(ref _audioLevel, value);
    }
    
    public string ConnectionInfo
    {
        get => _connectionInfo;
        set => this.RaiseAndSetIfChanged(ref _connectionInfo, value);
    }
    
    public ObservableCollection<DeviceInfo> Devices { get; }
    
    public ReactiveCommand<Unit, Unit> StartServerCommand { get; }
    public ReactiveCommand<Unit, Unit> StopServerCommand { get; }
    public ReactiveCommand<Unit, Unit> ConnectToDeviceCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshDevicesCommand { get; }
    
    private async Task StartServerAsync()
    {
        try
        {
            await _audioInjector.InitializeAsync(48000, 1, 16);
            await _audioInjector.StartAsync();
            
            _streamingService.StartServer();
            
            StatusMessage = "Server started. Waiting for Android phone...";
            _ = Task.Run(RefreshDevicesPeriodically);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    private async Task StopServerAsync()
    {
        try
        {
            _streamingService.Disconnect();
            await _audioInjector.StopAsync();
            
            StatusMessage = "Server stopped";
            IsStreaming = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    private async Task ConnectToDeviceAsync()
    {
        if (string.IsNullOrEmpty(SelectedDeviceId))
            return;
            
        var device = Devices.FirstOrDefault(d => d.DeviceId == SelectedDeviceId);
        if (device == null)
            return;
            
        try
        {
            StatusMessage = $"Connecting to {device.DeviceName}...";
            
            bool connected = await _streamingService.ConnectToServerAsync(
                device.IpAddress, 
                device.Port);
            
            if (connected)
            {
                StatusMessage = $"Connected to {device.DeviceName}";
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
            _streamingService.Disconnect();
            await _audioInjector.StopAsync();
            
            StatusMessage = "Disconnected";
            IsStreaming = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    private void RefreshDevices()
    {
        var devices = _streamingService.GetDiscoveredDevices();
        
        Devices.Clear();
        foreach (var device in devices.Values)
        {
            Devices.Add(device);
        }
    }
    
    private async Task RefreshDevicesPeriodically()
    {
        while (true)
        {
            await Task.Delay(2000);
            RefreshDevices();
        }
    }
    
    private void OnConnectionStateChanged(ConnectionState state)
    {
        IsStreaming = state.Status == ConnectionStatus.Streaming;
        
        ConnectionInfo = state.IsConnected
            ? $"Connected to {state.RemoteAddress}:{state.RemotePort}\n" +
              $"Packets: {state.PacketsReceived} received, {state.PacketsSent} sent\n" +
              $"Data: {FormatBytes(state.BytesReceived)} received\n" +
              $"Loss: {state.PacketLossRate:P1}"
            : "Not connected";
        
        if (!string.IsNullOrEmpty(state.ErrorMessage))
        {
            StatusMessage = state.ErrorMessage;
        }
    }
    
    private async void OnAudioDataReceived(byte[] audioData)
    {
        await _audioInjector.WriteAudioAsync(audioData);
        
        AudioLevel = CalculateAudioLevel(audioData);
    }
    
    private void OnError(Exception ex)
    {
        StatusMessage = $"Error: {ex.Message}";
    }
    
    private double CalculateAudioLevel(byte[] pcmData)
    {
        if (pcmData.Length < 2)
            return 0.0;
            
        long sum = 0;
        int sampleCount = pcmData.Length / 2;
        
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
            sum += sample * sample;
        }
        
        double rms = Math.Sqrt((double)sum / sampleCount);
        double normalized = rms / 32768.0; 
        
        return Math.Min(normalized * 100, 100);
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