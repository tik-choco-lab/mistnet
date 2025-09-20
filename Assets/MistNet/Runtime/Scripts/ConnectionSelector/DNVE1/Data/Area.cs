using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class Area
    {
        public const int ChunkSize = 256;
        [JsonProperty("x")] public int X;
        [JsonProperty("y")] public int Y;
        [JsonProperty("z")] public int Z;

        public Area()
        {
        }

        public Area(Vector3 position)
        {
            X = Mathf.FloorToInt(position.x / ChunkSize);
            // Y = Mathf.FloorToInt(position.y / ChunkSize);
            Y = 0;
            Z = Mathf.FloorToInt(position.z / ChunkSize);
        }

        public Area(int x, int y, int z)
        {
            X = x;
            // Y = y;
            Y = 0;
            Z = z;
        }

        public void Set(Vector3 position)
        {
            X = Mathf.FloorToInt(position.x / ChunkSize);
            Y = 0;
            Z = Mathf.FloorToInt(position.z / ChunkSize);
        }

        public void Set((int, int, int) chunk)
        {
            X = chunk.Item1;
            Y = chunk.Item2;
            Z = chunk.Item3;
        }

        public (int, int, int) GetChunk()
        {
            return (X, Y, Z);
        }

        public override string ToString()
        {
            return $"{X},{Y},{Z}";
        }

        public override bool Equals(object obj)
        {
            if (obj is Area other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public static Vector3Int ToChunk(Vector3 position)
        {
            return new Vector3Int(
                Mathf.FloorToInt(position.x / ChunkSize),
                0,
                Mathf.FloorToInt(position.z / ChunkSize)
            );
        }
    }

    public class AreaInfo
    {
        [JsonProperty("nodes")] public HashSet<NodeId> Nodes { get; set; } = new ();
        [JsonProperty("expireAt")] public Dictionary<NodeId, DateTime> ExpireAt { get; set; } = new ();
    }
}
