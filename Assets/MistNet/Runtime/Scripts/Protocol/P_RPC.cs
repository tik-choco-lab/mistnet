using MemoryPack;

[MemoryPackable]
public partial class P_RPC
{
    public string Method { get; set; }
    public RpcArgBase[] Args { get; set; }
}

[MemoryPackUnion(0, typeof(RpcArgInt))]
[MemoryPackUnion(1, typeof(RpcArgFloat))]
[MemoryPackUnion(2, typeof(RpcArgString))]
[MemoryPackUnion(3, typeof(RpcArgBool))]
[MemoryPackUnion(4, typeof(RpcArgByteArray))]
[MemoryPackable]
public abstract partial class RpcArgBase { }

[MemoryPackable]
public partial class RpcArgInt : RpcArgBase
{
    public int Value;
    [MemoryPackConstructor]
    public RpcArgInt() { }
    public RpcArgInt(int value) => Value = value;
}

[MemoryPackable]
public partial class RpcArgFloat : RpcArgBase
{
    public float Value;
    [MemoryPackConstructor]
    public RpcArgFloat() { }
    public RpcArgFloat(float value) => Value = value;
}

[MemoryPackable]
public partial class RpcArgString : RpcArgBase
{
    public string Value;
    [MemoryPackConstructor]
    public RpcArgString() { }
    public RpcArgString(string value) => Value = value;
}

[MemoryPackable]
public partial class RpcArgBool : RpcArgBase
{
    public bool Value;
    [MemoryPackConstructor]
    public RpcArgBool() { }
    public RpcArgBool(bool value) => Value = value;
}

[MemoryPackable]
public partial class RpcArgByteArray : RpcArgBase
{
    public byte[] Value;
    [MemoryPackConstructor]
    public RpcArgByteArray() { }
    public RpcArgByteArray(byte[] value) => Value = value;
}
