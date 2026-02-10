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
        private const float DynamicMaxRangeFlag = -1f;
        private const float DefaultMaxRange = 1f;
        private const float MinValue = 0f;
        private const float MaxCosineValue = 1f;
        private const float MinCosineValue = -1f;

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
            if (count <= MinValue) count = DefaultDirectionCount;
            _directions = GenerateUniformDirections(count);
        }

        private static Vector3[] GenerateUniformDirections(int count)
        {
            var directions = new Vector3[count];
            if (count == (int)MaxCosineValue)
            {
                directions[0] = Vector3.forward;
                return directions;
            }

            var phi = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (var i = 0; i < count; i++)
            {
                var y = MaxCosineValue - (i / (float)(count - 1)) * 2;
                var radius = Mathf.Sqrt(MaxCosineValue - y * y);
                var theta = phi * i;

                var x = Mathf.Cos(theta) * radius;
                var z = Mathf.Sin(theta) * radius;

                directions[i] = new Vector3(x, y, z);
            }
            return directions;
        }

        public static float[,] CreateSpatialDensity(Vector3 center, Vector3[] nodes, int nodeCount, float maxRange)
        {
            var layerCount = DefaultLayerCount;
            var densityMap = new float[Directions.Length, layerCount];
            CreateSpatialDensityInternal(center, nodes, nodeCount, densityMap, Directions, maxRange);
            return densityMap;
        }

        public static void CreateSpatialDensity(Vector3 center, Vector3[] nodes, int nodeCount, float[,] densityMap)
        {
            CreateSpatialDensityInternal(center, nodes, nodeCount, densityMap, Directions, DynamicMaxRangeFlag);
        }

        public static void CreateSpatialDensity(Vector3 center, Vector3[] nodes, int nodeCount, float[,] densityMap, float maxRange)
        {
            CreateSpatialDensityInternal(center, nodes, nodeCount, densityMap, Directions, maxRange);
        }

        private static void CreateSpatialDensityInternal(Vector3 center, Vector3[] nodes, int nodeCount, float[,] densityMap, Vector3[] directions,
            float maxRange)
        {
            var dirCount = directions.Length;
            var layerCount = densityMap.GetLength(1);
            System.Array.Clear(densityMap, 0, densityMap.Length);

            var safeNodeCount = Mathf.Min(nodeCount, nodes.Length);

            if (maxRange <= FloatComparisonThreshold)
            {
                maxRange = MinValue;
                for (int j = 0; j < safeNodeCount; j++)
                {
                    var dist = Vector3.Distance(center, nodes[j]);
                    if (dist > maxRange) maxRange = dist;
                }
                if (maxRange <= FloatComparisonThreshold) maxRange = DefaultMaxRange;
            }

            for (int j = 0; j < safeNodeCount; j++)
            {
                var node = nodes[j];
                var vec = node - center;
                var dist = vec.magnitude;
                var unitVec = vec.normalized;
                var layerIndex = Mathf.Min(Mathf.FloorToInt(dist / maxRange * layerCount), layerCount - 1);

                for (var i = 0; i < dirCount; i++)
                {
                    var score = Vector3.Dot(directions[i], unitVec);
                    if (score > MinValue)
                        densityMap[i, layerIndex] += score;
                }
            }
        }

        public static float[,] ProjectSpatialDensity(float[,] densityMap, Vector3 oldCenter, Vector3 newCenter)
        {
            var projected = new float[densityMap.GetLength(0), densityMap.GetLength(1)];
            ProjectSpatialDensity(densityMap, oldCenter, newCenter, projected, Directions);
            return projected;
        }

        public static void ProjectSpatialDensity(float[,] densityMap, Vector3 oldCenter, Vector3 newCenter, float[,] projected)
        {
            ProjectSpatialDensity(densityMap, oldCenter, newCenter, projected, Directions);
        }

        private static void ProjectSpatialDensity(float[,] densityMap, Vector3 oldCenter, Vector3 newCenter, float[,] projected,
            Vector3[] directions)
        {
            var offset = newCenter - oldCenter;
            if (offset.sqrMagnitude <= FloatComparisonThreshold * FloatComparisonThreshold)
            {
                System.Array.Copy(densityMap, projected, densityMap.Length);
                return;
            }

            var offsetUnit = offset.normalized;
            var dirCount = directions.Length;
            var layerCount = densityMap.GetLength(1);

            for (var i = 0; i < dirCount; i++)
            {
                var cosSim = Mathf.Clamp(Vector3.Dot(directions[i], offsetUnit), MinCosineValue, MaxCosineValue);
                for (var j = 0; j < layerCount; j++)
                {
                    projected[i, j] = densityMap[i, j] * (MaxCosineValue + ProjectionExpansionFactor * cosSim);
                    if (projected[i, j] < MinValue) projected[i, j] = MinValue;
                }
            }
        }

        public static float[,] MergeSpatialDensity(
            float[,] selfDensityMap, Vector3 selfCenter,
            float[,] otherDensityMap, Vector3 otherCenter)
        {
            var merged = new float[selfDensityMap.GetLength(0), selfDensityMap.GetLength(1)];
            MergeSpatialDensity(selfDensityMap, selfCenter, otherDensityMap, otherCenter, merged, Directions);
            return merged;
        }

        public static void MergeSpatialDensity(
            float[,] selfDensityMap, Vector3 selfCenter,
            float[,] otherDensityMap, Vector3 otherCenter,
            float[,] merged)
        {
            MergeSpatialDensity(selfDensityMap, selfCenter, otherDensityMap, otherCenter, merged, Directions);
        }
        
        private static float[,] _otherProjectedBuffer;

        private static void MergeSpatialDensity(
            float[,] selfDensityMap, Vector3 selfCenter,
            float[,] otherDensityMap, Vector3 otherCenter,
            float[,] merged,
            Vector3[] directions)
        {
            int dirs = directions.Length;
            var layerCount = selfDensityMap.GetLength(1);
            if (_otherProjectedBuffer == null || _otherProjectedBuffer.GetLength(0) != dirs || _otherProjectedBuffer.GetLength(1) != layerCount)
            {
                _otherProjectedBuffer = new float[dirs, layerCount];
            }
            
            ProjectSpatialDensity(otherDensityMap, otherCenter, selfCenter, _otherProjectedBuffer, directions);
            
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
