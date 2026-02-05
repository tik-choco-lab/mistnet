using System;
using System.ComponentModel;
using System.Security.Cryptography;
using MemoryPack;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    [Serializable]
    [MemoryPackable]
    public partial struct Position
    {
        public float x;
        public float y;
        public float z;

        public Position(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Position(Vector3 vector3)
        {
            x = vector3.x;
            y = vector3.y;
            z = vector3.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [MemoryPackable]
    public partial class Node
    {
        [MemoryPackOrder(0)] [JsonProperty("id")] public NodeId Id;
        [MemoryPackOrder(1)] [JsonProperty("position")] public Position Position;
        [MemoryPackOrder(2)] [JsonProperty("state")] public EvalNodeState State;

        [MemoryPackConstructor]
        public Node() { }

        public Node(NodeId nodeId, Position position)
        {
            Id = nodeId;
            Position = position;
        }

        public Node(NodeId nodeId, Position position, EvalNodeState state)
        {
            Id = nodeId;
            Position = position;
            State = state;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (Node)obj;
            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    [JsonConverter(typeof(NodeIdConverter))]
    [TypeConverter(typeof(NodeIdTypeConverter))]
    [MemoryPackable]
    public partial record NodeId(string Id)
    {
        public string Id { get; } = Id;
        public override string ToString() => Id;
        public static implicit operator string(NodeId nodeId) => nodeId?.Id;
    }

    public record ObjectId(string Id)
    {
        public string Id { get; } = Id;
        public override string ToString() => Id;
        public static implicit operator string(ObjectId objectId) => objectId.Id;
    }

    public class NodeIdConverter : JsonConverter<NodeId>
    {
        public NodeIdConverter()
        {
        }

        public override void WriteJson(JsonWriter writer, NodeId value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override NodeId ReadJson(JsonReader reader, Type objectType, NodeId existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            var nodeId = (string)reader.Value;
            return new NodeId(nodeId);
        }
    }

    public class NodeIdTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string)) return true;
            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture,
            object value)
        {
            if (value is string str) return new NodeId(str); // Convert string to NodeId
            return base.ConvertFrom(context, culture, value);
        }
    }
}
