using System;
using System.Threading.Tasks;

namespace AndroidMicSystem.Desktop.AudioInjection;


public interface IPlatformAudioInjector : IDisposable
{
 
    Task<bool> InitializeAsync(int sampleRate, int channels, int bitsPerSample);
    
    
    Task<bool> StartAsync();
    
 
    Task StopAsync();
    
   
    Task WriteAudioAsync(byte[] audioData);
    
    
    bool IsRunning { get; }
    
  
    string DeviceName { get; }
}