using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AndroidMicSystem.Desktop.AudioInjection;


public class PipeWireAudioInjector : IPlatformAudioInjector
{
    private Process? _pwCatProcess;
    private StreamWriter? _audioStreamWriter;
    private bool _isRunning;
    private int _sampleRate;
    private int _channels;
    private int _bitsPerSample;
    
    public bool IsRunning => _isRunning;
    public string DeviceName => "AndroidMic Virtual Input";
    
    public async Task<bool> InitializeAsync(int sampleRate, int channels, int bitsPerSample)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _bitsPerSample = bitsPerSample;
        
        if (!await CheckPipeWireAvailableAsync())
        {
            Console.WriteLine("PipeWire not available. Please install pipewire and pipewire-pulse.");
            return false;
        }
        
        return true;
    }
    
    public async Task<bool> StartAsync()
    {
        if (_isRunning)
            return true;
            
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pw-cat",
                Arguments = BuildPwCatArguments(),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            _pwCatProcess = Process.Start(startInfo);
            
            if (_pwCatProcess == null)
            {
                Console.WriteLine("Failed to start pw-cat process");
                return false;
            }
            
            _audioStreamWriter = new StreamWriter(_pwCatProcess.StandardInput.BaseStream, Encoding.ASCII);
            _audioStreamWriter.AutoFlush = false;
            
            _isRunning = true;
            
            _ = Task.Run(MonitorProcessErrors);
            
            Console.WriteLine($"PipeWire virtual microphone started: {DeviceName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start PipeWire injector: {ex.Message}");
            return false;
        }
    }
    
    public async Task StopAsync()
    {
        if (!_isRunning)
            return;
            
        _isRunning = false;
        
        try
        {
            _audioStreamWriter?.Close();
            _pwCatProcess?.Kill();
            _pwCatProcess?.WaitForExit(1000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping PipeWire injector: {ex.Message}");
        }
        finally
        {
            _audioStreamWriter?.Dispose();
            _pwCatProcess?.Dispose();
            _audioStreamWriter = null;
            _pwCatProcess = null;
        }
        
        await Task.CompletedTask;
    }
    
    public async Task WriteAudioAsync(byte[] audioData)
    {
        if (!_isRunning || _audioStreamWriter == null || _pwCatProcess?.HasExited == true)
        {
            return;
        }
        
        try
        {
          
            await _audioStreamWriter.BaseStream.WriteAsync(audioData, 0, audioData.Length);
            await _audioStreamWriter.BaseStream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing audio data: {ex.Message}");
            _isRunning = false;
        }
    }
    
    private string BuildPwCatArguments()
    {
    
        
        string format = _bitsPerSample == 16 ? "s16" : "s32"; // signed 16-bit or 32-bit
        
        return $"--playback " +
               $"--media-type=Audio " +
               $"--media-category=Capture " +
               $"--media-role=Communication " +
               $"--rate={_sampleRate} " +
               $"--channels={_channels} " +
               $"--format={format} " +
               $"--media-name=\"{DeviceName}\" " +
               $"-";
    }
    
    private async Task<bool> CheckPipeWireAvailableAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "pw-cat",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(startInfo);
            if (process == null)
                return false;
                
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task MonitorProcessErrors()
    {
        if (_pwCatProcess?.StandardError == null)
            return;
            
        try
        {
            string? line;
            while ((line = await _pwCatProcess.StandardError.ReadLineAsync()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Console.WriteLine($"PipeWire error: {line}");
                }
            }
        }
        catch
        {
         
        }
    }
    
    public void Dispose()
    {
        StopAsync().Wait();
    }
}