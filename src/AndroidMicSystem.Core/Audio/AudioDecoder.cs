using System.Collections.Concurrent;

namespace AndroidMicSystem.Core.Audio;


public class AudioDecoder
{
    private readonly ConcurrentDictionary<uint, AudioPacket> _jitterBuffer = new();
    private readonly int _jitterBufferSize;
    private uint _expectedSequence = 0;
    private uint _lastProcessedSequence = 0;
    private bool _isFirstPacket = true;
    
    public AudioSettings Settings { get; private set; }
    public int PacketsLost { get; private set; }
    public int PacketsReceived { get; private set; }
    
    public AudioDecoder(int jitterBufferSize = 5)
    {
        _jitterBufferSize = jitterBufferSize;
        Settings = new AudioSettings();
    }
    
    public void ReceivePacket(AudioPacket packet)
    {
        PacketsReceived++;
        
        if (_isFirstPacket)
        {
            _isFirstPacket = false;
            _expectedSequence = packet.SequenceNumber;
            Settings = new AudioSettings
            {
                SampleRate = packet.SampleRate,
                Channels = packet.Channels,
                BitsPerSample = packet.BitsPerSample
            };
        }
        
        _jitterBuffer.TryAdd(packet.SequenceNumber, packet);
        
        CleanOldPackets();
    }
    
    public AudioPacket? GetNextPacket()
    {
        if (_jitterBuffer.Count < _jitterBufferSize && !_isFirstPacket)
            return null;
            
        if (_jitterBuffer.TryRemove(_expectedSequence, out var packet))
        {
            _lastProcessedSequence = _expectedSequence;
            _expectedSequence++;
            return packet;
        }
        
        uint oldestSequence = _jitterBuffer.Keys.DefaultIfEmpty(uint.MaxValue).Min();
        
        if (oldestSequence != uint.MaxValue && IsSequenceAhead(oldestSequence, _expectedSequence, 10))
        {
            uint skipped = SequenceDifference(_expectedSequence, oldestSequence);
            PacketsLost += (int)skipped;
            _expectedSequence = oldestSequence;
            
            if (_jitterBuffer.TryRemove(_expectedSequence, out packet))
            {
                _lastProcessedSequence = _expectedSequence;
                _expectedSequence++;
                return packet;
            }
        }
        
        return null;
    }
    
    public double GetPacketLossRate()
    {
        int total = PacketsReceived + PacketsLost;
        return total > 0 ? (double)PacketsLost / total : 0.0;
    }
    
    private void CleanOldPackets()
    {

        var oldPackets = _jitterBuffer.Keys.Where(seq => 
            IsSequenceBehind(seq, _lastProcessedSequence, 100)).ToList();
            
        foreach (var seq in oldPackets)
        {
            _jitterBuffer.TryRemove(seq, out _);
        }
    }
    
    private bool IsSequenceAhead(uint seq1, uint seq2, uint maxDifference)
    {
        uint diff = SequenceDifference(seq2, seq1);
        return diff > 0 && diff < maxDifference;
    }
    
    private bool IsSequenceBehind(uint seq1, uint seq2, uint maxDifference)
    {
        uint diff = SequenceDifference(seq1, seq2);
        return diff > 0 && diff < maxDifference;
    }
    
    private uint SequenceDifference(uint seq1, uint seq2)
    {
        if (seq2 >= seq1)
            return seq2 - seq1;
        else
            return (uint.MaxValue - seq1) + seq2 + 1;
    }
}