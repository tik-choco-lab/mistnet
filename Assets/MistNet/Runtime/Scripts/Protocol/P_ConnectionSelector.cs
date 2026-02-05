using MemoryPack;
using UnityEngine;

[MemoryPackable]
public partial class P_ConnectionSelector
{
    [MemoryPackOrder(0)] public string Data { get; set; }
    [MemoryPackOrder(1)] public byte[] RawData { get; set; }
}
