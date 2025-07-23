using UnityEngine;

namespace MistNet
{
    public class Area
    {
        private const int ChunkSize = 16;
        public int X;
        public int Y;
        public int Z;

        public Area(Vector3 position)
        {
            X = Mathf.FloorToInt(position.x / ChunkSize);
            Y = Mathf.FloorToInt(position.y / ChunkSize);
            Z = Mathf.FloorToInt(position.z / ChunkSize);
        }

        public Area(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
