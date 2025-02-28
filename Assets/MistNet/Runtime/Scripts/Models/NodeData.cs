using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{

    [Serializable]
    public struct Position
    {
        private const int ChunkSize = 16;
        private const float ChunkSizeDivide = 1f / ChunkSize;
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

        public Chunk ToChunk => new
        (
            Mathf.FloorToInt(x * ChunkSizeDivide),
            Mathf.FloorToInt(y * ChunkSizeDivide),
            Mathf.FloorToInt(z * ChunkSizeDivide)
        );

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    public struct Chunk
    {
        public int x;
        public int y;
        public int z;

        public Chunk(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (Chunk)obj;
            return x == other.x && y == other.y && z == other.z;
        }

        public bool Equals(Chunk other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y, z);
        }
    }

    public struct Node
    {
        public Chunk Chunk; // 1,2,-1 のような形式
        public NodeId Id;
        public Position Position;
        public DateTime LastUpdate;

        public Node(NodeId nodeId, Position position)
        {
            Id = nodeId;
            Position = position;
            Chunk = position.ToChunk;
            LastUpdate = DateTime.Now;
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

        public bool Equals(Node other)
        {
            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    [JsonConverter(typeof(NodeIdConverter))]
    public readonly struct NodeId
    {
        private readonly string _nodeId;

        public NodeId(string nodeId)
        {
            _nodeId = nodeId;
        }

        public override string ToString()
        {
            return _nodeId;
        }

        public static implicit operator string(NodeId nodeId)
        {
            return nodeId.ToString();
        }

        public static implicit operator NodeId(string nodeId)
        {
            return new NodeId(nodeId);
        }

        public static bool operator ==(NodeId a, NodeId b)
        {
            return a._nodeId == b._nodeId;
        }

        public static bool operator !=(NodeId a, NodeId b)
        {
            return a._nodeId != b._nodeId;
        }

        public override bool Equals(object obj)
        {
            return obj is NodeId other && _nodeId == other._nodeId;
        }

        public override int GetHashCode()
        {
            return _nodeId.GetHashCode();
        }
    }

    public class NodeIdConverter : JsonConverter<NodeId>
    {
        public override void WriteJson(JsonWriter writer, NodeId value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override NodeId ReadJson(JsonReader reader, Type objectType, NodeId existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var nodeId = (string)reader.Value;
            return new NodeId(nodeId);
        }
    }

    [Serializable]
    public class CheckAllNodes
    {
        public string type;
        public string id;
        public Dictionary<string, Node> nodes;

        public CheckAllNodes(string type, string id, Dictionary<string, Node> nodes)
        {
            this.type = type;
            this.id = id;
            this.nodes = nodes;
        }
    }
}
