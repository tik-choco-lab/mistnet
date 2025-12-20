using MemoryPack;
using UnityEngine;

[MemoryPackable]
public partial class P_Location
{
    public string ObjId { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public Vector3 Velocity { get; set; }
    public float Time { get; set; }        
    public ushort Sequence { get; set; } // 順序逆転対策（2バイト、0〜65535でラップ）
}
