using MemoryPack;

[MemoryPackable]
public partial class MistMessage
{
    public MistNetMessageType Type;
    public string TargetId;
    public string Id;
    public byte[] Payload;
    public int HopCount;
}

namespace MistNet
{
    public struct MessageInfo
    {
        public NodeId SourceId { get; set; }
        public NodeId SenderId { get; set; }
    }
}
