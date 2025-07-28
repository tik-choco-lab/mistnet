using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class Area
    {
        private const int ChunkSize = 128;
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

        protected bool Equals(Area other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }
    }

    public class AreaInfo
    {
        [JsonProperty("chunk")] public Area Chunk { get; set; }
        [JsonProperty("nodes")] public HashSet<NodeInfo> Nodes { get; set; } = new ();
    }
}
