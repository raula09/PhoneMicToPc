using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AndroidMicSystem.Desktop.AudioInjection;

public class PipeWireAudioInjector : IDisposable
{
    private Process? _pwCatProcess;
    private StreamWriter? _audioStreamWriter;
    private bool _isRunning;
    
    public bool IsRunning => _isRunning;
    public string DeviceName => "AndroidMic Virtual Input";
    
    public async Task<bool> InitializeAsync(int sampleRate, int channels, int bitsPerSample)
    { 
        try
        {
            var check = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "pw-cat",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            
            if (check == null)
                return false;
                
            await check.WaitForExitAsync();
            return check.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
    
    public Task<bool> StartAsync()
    {
        if (_isRunning)
            return Task.FromResult(true);
            
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pw-cat",
                Arguments = "--playback --raw --media-type=Audio --media-category=Capture " +
                           "--media-role=Communication --rate=48000 --channels=1 --format=s16 " +
                           "-P media.name=\"AndroidMic Virtual Input\" -",
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            _pwCatProcess = Process.Start(startInfo);
            
            if (_pwCatProcess == null)
                return Task.FromResult(false);
            
            _audioStreamWriter = new StreamWriter(_pwCatProcess.StandardInput.BaseStream);
            _isRunning = true;
            
            Console.WriteLine($"PipeWire virtual microphone started: {DeviceName}");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start PipeWire: {ex.Message}");
            return Task.FromResult(false);
        }
    }
    
    public async Task StopAsync()
    {
        if (!_isRunning)
            return;
            
        _isRunning = false;
        
        try
        {
            if (_audioStreamWriter != null)
            {
                await _audioStreamWriter.FlushAsync();
                _audioStreamWriter.Close();
            }
            
            if (_pwCatProcess != null && !_pwCatProcess.HasExited)
            {
                _pwCatProcess.Kill();
                await _pwCatProcess.WaitForExitAsync();
            }
        }
        catch { }
        finally
        {
            _audioStreamWriter?.Dispose();
            _pwCatProcess?.Dispose();
            _audioStreamWriter = null;
            _pwCatProcess = null;
        }
    }
    
    public async Task WriteAudioAsync(byte[] audioData)
    {
        if (!_isRunning || _audioStreamWriter == null || _pwCatProcess?.HasExited == true)
            return;
        
        try
        {
            await _audioStreamWriter.BaseStream.WriteAsync(audioData);
            await _audioStreamWriter.BaseStream.FlushAsync();
        }
        catch
        {
            _isRunning = false;
        }
    }
    
    public void Dispose()
    {
        StopAsync().Wait();
    }
}