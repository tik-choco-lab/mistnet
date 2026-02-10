using System.Runtime.InteropServices;
using MistNet.DNVE3;
using UnityEngine;

namespace MistNet.Utils
{
    public static class SpatialDensityUtils
    {
        public const int DefaultLayerCount = 4;
        private const int DefaultDirectionCount = 26;
        private const float FloatComparisonThreshold = 0.00001f;
        private const float ByteScalingFactor = 255.0f;
        private const float ProjectionExpansionFactor = 0.5f;

        private static Vector3[] _directions;
        public static Vector3[] Directions
        {
            get
            {
                if (_directions != null) return _directions;
                Initialize(DefaultDirectionCount);
                return _directions;
            }
        }

        public static void Initialize(int count)
        {
            if (count <= 0) count = DefaultDirectionCount;
            _directions = GenerateUniformDirections(count);
        }

        private static Vector3[] GenerateUniformDirections(int count)
        {
            var directions = new Vector3[count];
            if (count == 1)
            {
                directions[0] = Vector3.forward;
                return directions;
            }

            var phi = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (var i = 0; i < count; i++)
            {
                var y = 1 - (i / (float)(count - 1)) * 2;
                var radius = Mathf.Sqrt(1 - y * y);
                var theta = phi * i;

                var x = Mathf.Cos(theta) * radius;
                var z = Mathf.Sin(theta) * radius;

                directions[i] = new Vector3(x, y, z);
            }
            return directions;
        }

        public static float[,] CreateSpatialDensity(Vector3 center, Vector3[] nodes, int nodeCount, int layerCount = DefaultLayerCount)
        {
            var densityMap = new float[Directions.Length, layerCount];
            CreateSpatialDensity(center, nodes, nodeCount, densityMap, Directions, layerCount);
            return densityMap;
        }

        public static void CreateSpatialDensity(Vector3 center, Vector3[] nodes, int nodeCount, float[,] densityMap, int layerCount = DefaultLayerCount)
        {
            CreateSpatialDensity(center, nodes, nodeCount, densityMap, Directions, layerCount);
        }

        private static void CreateSpatialDensity(Vector3 center, Vector3[] nodes, int nodeCount, float[,] densityMap, Vector3[] directions,
            int layerCount = DefaultLayerCount)
        {
            var dirCount = directions.Length;
            System.Array.Clear(densityMap, 0, densityMap.Length);
            var maxDist = 0f;

            for (int i = 0; i < nodeCount; i++)
            {
                var node = nodes[i];
                var dist = Vector3.Distance(center, node);
                if (dist > maxDist) maxDist = dist;
            }

            if (maxDist == 0f) maxDist = 1f;

            for (int j = 0; j < nodeCount; j++)
            {
                var node = nodes[j];
                var vec = node - center;
                var dist = vec.magnitude;
                var unitVec = vec.normalized;
                var layerIndex = Mathf.Min(Mathf.FloorToInt(dist / maxDist * layerCount), layerCount - 1);

                for (var i = 0; i < dirCount; i++)
                {
                    var score = Vector3.Dot(directions[i], unitVec);
                    if (score > 0f)
                        densityMap[i, layerIndex] += score;
                }
            }
        }

        public static float[,] ProjectSpatialDensity(float[,] densityMap, Vector3 oldCenter, Vector3 newCenter,
            int layerCount = DefaultLayerCount)
        {
            var projected = new float[densityMap.GetLength(0), densityMap.GetLength(1)];
            ProjectSpatialDensity(densityMap, oldCenter, newCenter, projected, Directions, layerCount);
            return projected;
        }

        public static void ProjectSpatialDensity(float[,] densityMap, Vector3 oldCenter, Vector3 newCenter, float[,] projected,
            int layerCount = DefaultLayerCount)
        {
            ProjectSpatialDensity(densityMap, oldCenter, newCenter, projected, Directions, layerCount);
        }

        private static void ProjectSpatialDensity(float[,] densityMap, Vector3 oldCenter, Vector3 newCenter, float[,] projected,
            Vector3[] directions, int layerCount = DefaultLayerCount)
        {
            var offset = newCenter - oldCenter;
            var offsetNorm = offset.magnitude;
            if (offsetNorm == 0f)
            {
                System.Array.Copy(densityMap, projected, densityMap.Length);
                return;
            }

            var offsetUnit = offset.normalized;
            var dirCount = directions.Length;

            for (var i = 0; i < dirCount; i++)
            {
                var cosSim = Mathf.Clamp(Vector3.Dot(directions[i], offsetUnit), -1f, 1f);
                for (var j = 0; j < layerCount; j++)
                {
                    projected[i, j] = densityMap[i, j] * (1f + ProjectionExpansionFactor * cosSim);
                    if (projected[i, j] < 0f) projected[i, j] = 0f;
                }
            }
        }

        public static float[,] MergeSpatialDensity(
            float[,] selfDensityMap, Vector3 selfCenter,
            float[,] otherDensityMap, Vector3 otherCenter,
            int layerCount = DefaultLayerCount)
        {
            var merged = new float[selfDensityMap.GetLength(0), selfDensityMap.GetLength(1)];
            MergeSpatialDensity(selfDensityMap, selfCenter, otherDensityMap, otherCenter, merged, Directions, layerCount);
            return merged;
        }

        public static void MergeSpatialDensity(
            float[,] selfDensityMap, Vector3 selfCenter,
            float[,] otherDensityMap, Vector3 otherCenter,
            float[,] merged,
            int layerCount = DefaultLayerCount)
        {
            MergeSpatialDensity(selfDensityMap, selfCenter, otherDensityMap, otherCenter, merged, Directions, layerCount);
        }
        
        private static float[,] _otherProjectedBuffer;

        private static void MergeSpatialDensity(
            float[,] selfDensityMap, Vector3 selfCenter,
            float[,] otherDensityMap, Vector3 otherCenter,
            float[,] merged,
            Vector3[] directions,
            int layerCount = DefaultLayerCount)
        {
            int dirs = directions.Length;
            if (_otherProjectedBuffer == null || _otherProjectedBuffer.GetLength(0) != dirs || _otherProjectedBuffer.GetLength(1) != layerCount)
            {
                _otherProjectedBuffer = new float[dirs, layerCount];
            }
            
            ProjectSpatialDensity(otherDensityMap, otherCenter, selfCenter, _otherProjectedBuffer, directions, layerCount);
            
            for (var i = 0; i < dirs; i++)
            {
                for (var j = 0; j < layerCount; j++)
                {
                    merged[i, j] = selfDensityMap[i, j] + _otherProjectedBuffer[i, j];
                }
            }
        }

        public static SpatialDensityDataByte ToCompact(SpatialDensityData original)
        {
            var densityMaps = original.DensityMap;
            var directionsCount = densityMaps.GetLength(0);
            var layerCount = densityMaps.GetLength(1);
            var result = new byte[directionsCount * layerCount];
            return ToCompact(original, result);
        }

        public static SpatialDensityDataByte ToCompact(SpatialDensityData original, byte[] resultBuffer)
        {
            var densityMaps = original.DensityMap;
            var densitySpan = MemoryMarshal.CreateSpan(ref densityMaps[0, 0], densityMaps.Length);
            float maxVal = FloatComparisonThreshold;
            foreach (var val in densitySpan) if (val > maxVal) maxVal = val;

            float invMax = ByteScalingFactor / maxVal;
            for (int i = 0; i < densitySpan.Length; i++)
            {
                resultBuffer[i] = (byte)(densitySpan[i] * invMax);
            }

            return new SpatialDensityDataByte
            {
                Position = original.Position.ToVector3(),
                MaxValue = maxVal,
                ByteDensities = resultBuffer
            };
        }

        public static SpatialDensityData FromCompact(SpatialDensityDataByte compact, int layerCount)
        {
            var directionsCount = Directions.Length;
            var densityMaps = new float[directionsCount, layerCount];
            var densitySpan = MemoryMarshal.CreateSpan(ref densityMaps[0, 0], densityMaps.Length);

            float maxVal = compact.MaxValue;
            for (int i = 0; i < densitySpan.Length; i++)
            {
                densitySpan[i] = (compact.ByteDensities[i] / ByteScalingFactor) * maxVal;
            }

            return new SpatialDensityData
            {
                DensityMap = densityMaps,
                Position = new Position(compact.Position)
            };
        }
    }
}
