namespace AndroidMicSystem.Core.Audio;

public class AudioSettings
{
    public int SampleRate { get; set; } = 48000;
    public int Channels { get; set; } = 1;
    public int BitsPerSample { get; set; } = 16;
    public float Gain { get; set; } = 1.0f;
    public int BytesPerSecond => SampleRate * Channels * (BitsPerSample / 8);
    public string FormatDescription => $"{SampleRate}Hz {Channels}ch {BitsPerSample}bit";
}
