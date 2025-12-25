using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AndroidMicSystem.Core.Audio;
using AndroidMicSystem.Core.Network;
using AndroidMicSystem.Desktop.AudioInjection;

namespace AndroidMicSystem.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly UdpAudioStreamer _audioStreamer;
    private readonly PipeWireAudioInjector _audioInjector;
    
    [ObservableProperty]
    private string _statusMessage = "Ready to start";
    
    [ObservableProperty]
    private double _audioLevel = 0;
    
    [ObservableProperty]
    private long _packetsReceived = 0;
    
    [ObservableProperty]
    private long _bytesReceived = 0;
    
    [ObservableProperty]
    private bool _isServerRunning = false;
    
    public MainViewModel()
    {
        _audioStreamer = new UdpAudioStreamer(5000);
        _audioInjector = new PipeWireAudioInjector();
        
        _audioStreamer.PacketReceived += OnPacketReceived;
        _audioStreamer.Error += OnError;
    }
    
    [RelayCommand]
    private async Task StartServer()
    {
        try
        {
            StatusMessage = "Initializing audio injector...";
            
            if (!await _audioInjector.InitializeAsync(48000, 1, 16))
            {
                StatusMessage = "Error: PipeWire not available. Install pw-cat.";
                return;
            }
            
            if (!await _audioInjector.StartAsync())
            {
                StatusMessage = "Error: Failed to start PipeWire";
                return;
            }
            
            _audioStreamer.StartReceiving();
            
            IsServerRunning = true;
            StatusMessage = "Server running - send audio to port 5000";
            PacketsReceived = 0;
            BytesReceived = 0;
            AudioLevel = 0;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private async Task StopServer()
    {
        try
        {
            _audioStreamer.StopReceiving();
            await _audioInjector.StopAsync();
            
            IsServerRunning = false;
            StatusMessage = "Server stopped";
            AudioLevel = 0;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    private async void OnPacketReceived(AudioPacket packet)
    { 
        await _audioInjector.WriteAudioAsync(packet.AudioData);
         
        PacketsReceived++;
        BytesReceived += packet.AudioData.Length;
         
        double level = CalculateAudioLevel(packet.AudioData);
        AudioLevel = level;
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
}