using MemoryPack;

[MemoryPackable]
public partial class MistMessage
{
    public MistNetMessageType Type;
    public string TargetId;
    public string Id;
    public byte[] Payload;
}

namespace MistNet
{
    public struct MessageInfo
    {
        public string SourceId { get; set; }
        public string SenderId { get; set; }
    }
}
