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
    
    public static AudioPacket? FromBytes(byte[] buffer)
    {
        if (buffer.Length < HeaderSize)
            return null;
            
        int offset = 0;
        var packet = new AudioPacket
        {
            Magic = ReadUInt32(buffer, ref offset),
            SequenceNumber = ReadUInt32(buffer, ref offset),
            Timestamp = ReadUInt32(buffer, ref offset),
            SampleRate = ReadUInt16(buffer, ref offset),
            Channels = buffer[offset++],
            BitsPerSample = buffer[offset++]
        };
        
        if (packet.Magic != MagicNumber)
            return null;
        
        int audioDataLength = buffer.Length - HeaderSize;
        if (audioDataLength > 0)
        {
            packet.AudioData = new byte[audioDataLength];
            Array.Copy(buffer, HeaderSize, packet.AudioData, 0, audioDataLength);
        }
        
        return packet;
    }
    
    private static void WriteUInt32(byte[] buffer, ref int offset, uint value)
    {
        buffer[offset++] = (byte)(value & 0xFF);
        buffer[offset++] = (byte)((value >> 8) & 0xFF);
        buffer[offset++] = (byte)((value >> 16) & 0xFF);
        buffer[offset++] = (byte)((value >> 24) & 0xFF);
    }
    
    private static void WriteUInt16(byte[] buffer, ref int offset, ushort value)
    {
        buffer[offset++] = (byte)(value & 0xFF);
        buffer[offset++] = (byte)((value >> 8) & 0xFF);
    }
    
    private static uint ReadUInt32(byte[] buffer, ref int offset)
    {
        uint value = (uint)(buffer[offset++] |
                           (buffer[offset++] << 8) |
                           (buffer[offset++] << 16) |
                           (buffer[offset++] << 24));
        return value;
    }
    
    private static ushort ReadUInt16(byte[] buffer, ref int offset)
    {
        ushort value = (ushort)(buffer[offset++] | (buffer[offset++] << 8));
        return value;
    }
}
