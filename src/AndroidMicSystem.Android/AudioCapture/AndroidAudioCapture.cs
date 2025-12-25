using Android.Media;
using AndroidMicSystem.Core.Audio;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AndroidMicSystem.Android.AudioCapture;

public class AndroidAudioCapture : IDisposable
{
    private AudioRecord? _audioRecord;
    private bool _isRecording;
    private CancellationTokenSource? _cts;
    private readonly AudioSettings _settings;
    private readonly int _bufferSize;
    
    public event Action<byte[]>? AudioDataCaptured;
    public event Action<Exception>? Error;
    
    public bool IsRecording => _isRecording;
    
    public AndroidAudioCapture(AudioSettings settings)
    {
        _settings = settings;
        
        _bufferSize = AudioRecord.GetMinBufferSize(
            _settings.SampleRate,
            _settings.Channels == 1 ? ChannelIn.Mono : ChannelIn.Stereo,
            Encoding.Pcm16bit) * 2; 
    }
    
    public bool Initialize()
    {
        try
        {
            var channelConfig = _settings.Channels == 1 ? ChannelIn.Mono : ChannelIn.Stereo;
            var encoding = _settings.BitsPerSample == 16 ? Encoding.Pcm16bit : Encoding.Pcm8bit;
            
            _audioRecord = new AudioRecord(
                AudioSource.Mic,
                _settings.SampleRate,
                channelConfig,
                encoding,
                _bufferSize);
            
            if (_audioRecord.State != State.Initialized)
            {
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
            return false;
        }
    }
    
    public Task StartRecordingAsync()
    {
        if (_isRecording || _audioRecord == null)
            return Task.CompletedTask;
            
        try
        {
            _audioRecord.StartRecording();
            _isRecording = true;
            _cts = new CancellationTokenSource();
            
            return Task.Run(() => RecordingLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
            return Task.CompletedTask;
        }
    }
    
    public void StopRecording()
    {
        if (!_isRecording || _audioRecord == null)
            return;
            
        _isRecording = false;
        _cts?.Cancel();
        
        try
        {
            _audioRecord.Stop();
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
    }
    
    private void RecordingLoop(CancellationToken cancellationToken)
    {
        int chunkSize = (_settings.SampleRate / 50) * (_settings.BitsPerSample / 8) * _settings.Channels;
        byte[] buffer = new byte[chunkSize];
        
        while (_isRecording && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_audioRecord == null)
                    break;
                    
                int bytesRead = _audioRecord.Read(buffer, 0, buffer.Length);
                
                if (bytesRead > 0)
                {
                    byte[] audioData = new byte[bytesRead];
                    Array.Copy(buffer, audioData, bytesRead);
                    
                    AudioDataCaptured?.Invoke(audioData);
                }
                else if (bytesRead < 0)
                {
                    Error?.Invoke(new Exception($"AudioRecord read error: {bytesRead}"));
                    break;
                }
            }
            catch (Exception ex)
            {
                if (_isRecording)
                {
                    Error?.Invoke(ex);
                }
                break;
            }
        }
    }
    
    public void Dispose()
    {
        StopRecording();
        _audioRecord?.Release();
        _audioRecord?.Dispose();
        _cts?.Dispose();
    }
}