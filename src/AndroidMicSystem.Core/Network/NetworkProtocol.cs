namespace AndroidMicSystem.Core.Network;


public static class NetworkProtocol
{
 
    public const int DefaultAudioPort = 5000;
    public const int DefaultControlPort = 5001;
    public const int DefaultDiscoveryPort = 5002;
    
 
    public enum ControlMessageType : byte
    {
        Ping = 0,
        Pong = 1,
        StartStream = 2,
        StopStream = 3,
        StreamStarted = 4,
        StreamStopped = 5,
        DiscoveryRequest = 10,
        DiscoveryResponse = 11,
        Error = 255
    }
    
    
    public class ControlMessage
    {
        public ControlMessageType Type { get; set; }
        public string Payload { get; set; } = string.Empty;
        
        public byte[] ToBytes()
        {
            byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(Payload);
            byte[] buffer = new byte[1 + 4 + payloadBytes.Length];
            
            buffer[0] = (byte)Type;
            
            buffer[1] = (byte)(payloadBytes.Length & 0xFF);
            buffer[2] = (byte)((payloadBytes.Length >> 8) & 0xFF);
            buffer[3] = (byte)((payloadBytes.Length >> 16) & 0xFF);
            buffer[4] = (byte)((payloadBytes.Length >> 24) & 0xFF);
            
            Array.Copy(payloadBytes, 0, buffer, 5, payloadBytes.Length);
            
            return buffer;
        }
        
        public static ControlMessage? FromBytes(byte[] buffer)
        {
            if (buffer.Length < 5)
                return null;
                
            var message = new ControlMessage
            {
                Type = (ControlMessageType)buffer[0]
            };
            
            int payloadLength = buffer[1] | 
                              (buffer[2] << 8) | 
                              (buffer[3] << 16) | 
                              (buffer[4] << 24);
            
            if (buffer.Length >= 5 + payloadLength)
            {
                message.Payload = System.Text.Encoding.UTF8.GetString(buffer, 5, payloadLength);
            }
            
            return message;
        }
    }
}