namespace AndroidMicSystem.Core.Audio;


public class AudioPacket
{
    public const int HeaderSize = 16;
    public const uint MagicNumber = 0x414D5359;

    public uint Magic { get; set; } = MagicNumber;
    public uint SequenceNumber { get; set; }
    public uint Timestamp { get; set; }
    public ushort SampleRate { get; set; }
    public byte Channels { get; set; }
    public byte BitsPerSample { get; set; }


    public byte[] AudioData { get; set; } = Array.Empty<byte>();


    public byte[] ToBytes()
    {
        byte[] buffer = new byte[HeaderSize + AudioData.Length];
        int offset = 0;

        WriteUInt32(buffer, ref offset, Magic);
        WriteUInt32(buffer, ref offset, SequenceNumber);
        WriteUInt32(buffer, ref offset, Timestamp);
        WriteUInt16(buffer, ref offset, SampleRate);
        buffer[offset++] = Channels;
        buffer[offset++] = BitsPerSample;

        Array.Copy(AudioData, 0, buffer, offset, AudioData.Length);

        return buffer;
    }
}