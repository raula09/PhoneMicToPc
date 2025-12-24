namespace AndroidMicSystem.Core.Audio;

public class AudioEncoder
{
    private uint _sequenceNumber = 0;
    private readonly AudioSettings _settings;
    
    public AudioEncoder(AudioSettings settings)
    {
        _settings = settings;
    }
    
    public AudioPacket CreatePacket(byte[] pcmData, uint timestamp)
    {
        return new AudioPacket
        {
            SequenceNumber = _sequenceNumber++,
            Timestamp = timestamp,
            SampleRate = (ushort)_settings.SampleRate,
            Channels = (byte)_settings.Channels,
            BitsPerSample = (byte)_settings.BitsPerSample,
            AudioData = pcmData
        };
    }
    
    public int GetFrameSize()
    {
        int samplesPerFrame = _settings.SampleRate / 50;
        int bytesPerSample = (_settings.BitsPerSample / 8) * _settings.Channels;
        return samplesPerFrame * bytesPerSample;
    }
    
    public byte[] ConvertStereoToMono(byte[] stereoData)
    {
        if (_settings.Channels != 2 || _settings.BitsPerSample != 16)
            return stereoData;
            
        int sampleCount = stereoData.Length / 4; 
        byte[] monoData = new byte[sampleCount * 2];
        
        for (int i = 0; i < sampleCount; i++)
        {
            
            short left = (short)(stereoData[i * 4] | (stereoData[i * 4 + 1] << 8));
            short right = (short)(stereoData[i * 4 + 2] | (stereoData[i * 4 + 3] << 8));
            
            short mono = (short)((left + right) / 2);
            
            monoData[i * 2] = (byte)(mono & 0xFF);
            monoData[i * 2 + 1] = (byte)((mono >> 8) & 0xFF);
        }
        
        return monoData;
    }
    
    public void ApplyGain(byte[] pcmData, float gainMultiplier)
    {
        if (_settings.BitsPerSample != 16)
            return;
            
        int sampleCount = pcmData.Length / 2;
        
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
            
            int adjusted = (int)(sample * gainMultiplier);
            adjusted = Math.Clamp(adjusted, short.MinValue, short.MaxValue);
            
            pcmData[i * 2] = (byte)(adjusted & 0xFF);
            pcmData[i * 2 + 1] = (byte)((adjusted >> 8) & 0xFF);
        }
    }
}