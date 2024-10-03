using System;
using System.Collections.Generic;
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

    [Serializable]
    public class Node
    {
        public Chunk chunk; // 1,2,-1 のような形式
        public string id;
        public Position position;
        public string last_update;
        public DateTime LastUpdate => DateTime.Parse(last_update);

        public Node(Chunk chunk, Position position)
        {
            this.chunk = chunk;
            this.position = position;
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
