using System.Runtime.InteropServices;
using MistNet.DNVE3;
using UnityEngine;

namespace MistNet.Utils
{
    public static class SpatialDensityUtils
    {
        public const int DefaultDistBins = 4;
        private const int DefaultDirectionCount = 26; // 初期値（旧Level 3相当）

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

        /// <summary>
        /// 指定された方向数（count）に基づいてフィボナッチ格子を生成し、初期化する
        /// </summary>
        /// <param name="count">生成する方向ベクトルの数</param>
        public static void Initialize(int count)
        {
            if (count <= 0) count = DefaultDirectionCount;
            _directions = GenerateUniformDirections(count);
        }

        /// <summary>
        /// フィボナッチ格子（Fibonacci Sphere）アルゴリズムを用いて
        /// 球面上に均一な方向ベクトルを生成する
        /// </summary>
        private static Vector3[] GenerateUniformDirections(int count)
        {
            var directions = new Vector3[count];
            if (count == 1)
            {
                directions[0] = Vector3.forward;
                return directions;
            }

            var phi = Mathf.PI * (3f - Mathf.Sqrt(5f)); // 黄金角

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

        public static float[,] CreateSpatialDensity(Vector3 center, Vector3[] nodes, int distBins = DefaultDistBins)
        {
            return CreateSpatialDensity(center, nodes, Directions, distBins);
        }

        private static float[,] CreateSpatialDensity(Vector3 center, Vector3[] nodes, Vector3[] directions,
            int distBins = DefaultDistBins)
        {
            var dirCount = directions.Length;
            var hist = new float[dirCount, distBins];
            var maxDist = 0f;

            foreach (var node in nodes)
            {
                var dist = Vector3.Distance(center, node);
                if (dist > maxDist) maxDist = dist;
            }

            if (maxDist == 0f) maxDist = 1f;

            foreach (var node in nodes)
            {
                var vec = node - center;
                var dist = vec.magnitude;
                var unitVec = vec.normalized;
                var distIdx = Mathf.Min(Mathf.FloorToInt(dist / maxDist * distBins), distBins - 1);

                for (var i = 0; i < dirCount; i++)
                {
                    var score = Vector3.Dot(directions[i], unitVec);
                    if (score > 0f)
                        hist[i, distIdx] += score;
                }
            }

            return hist;
        }

        public static float[,] ProjectSpatialDensity(float[,] hist, Vector3 oldCenter, Vector3 newCenter,
            int distBins = DefaultDistBins)
        {
            return ProjectSpatialDensity(hist, oldCenter, newCenter, Directions, distBins);
        }

        private static float[,] ProjectSpatialDensity(float[,] hist, Vector3 oldCenter, Vector3 newCenter,
            Vector3[] directions, int distBins = DefaultDistBins)
        {
            var offset = newCenter - oldCenter;
            var offsetNorm = offset.magnitude;
            if (offsetNorm == 0f) return (float[,])hist.Clone();

            var offsetUnit = offset.normalized;
            var dirCount = directions.Length;
            var projected = (float[,])hist.Clone();

            for (var i = 0; i < dirCount; i++)
            {
                var cosSim = Mathf.Clamp(Vector3.Dot(directions[i], offsetUnit), -1f, 1f);
                for (var j = 0; j < distBins; j++)
                {
                    projected[i, j] *= 1f + 0.5f * cosSim;
                    if (projected[i, j] < 0f) projected[i, j] = 0f;
                }
            }

            return projected;
        }

        public static float[,] MergeSpatialDensity(
            float[,] selfHist, Vector3 selfCenter,
            float[,] otherHist, Vector3 otherCenter,
            int distBins = DefaultDistBins)
        {
            return MergeSpatialDensity(selfHist, selfCenter, otherHist, otherCenter, Directions, distBins);
        }

        private static float[,] MergeSpatialDensity(
            float[,] selfHist, Vector3 selfCenter,
            float[,] otherHist, Vector3 otherCenter,
            Vector3[] directions,
            int distBins = DefaultDistBins)
        {
            var otherProjected = ProjectSpatialDensity(otherHist, otherCenter, selfCenter, directions, distBins);
            var dirs = directions.Length;
            var merged = new float[dirs, distBins];

            for (var i = 0; i < dirs; i++)
            {
                for (var j = 0; j < distBins; j++)
                {
                    merged[i, j] = selfHist[i, j] + otherProjected[i, j];
                }
            }

            return merged;
        }

        public static SpatialDensityDataByte ToCompact(SpatialDensityData original)
        {
            var hists = original.DensityMap;
            var directionsCount = hists.GetLength(0);
            var binCount = hists.GetLength(1);
            var result = new byte[directionsCount * binCount];

            var histSpan = MemoryMarshal.CreateSpan(ref hists[0, 0], hists.Length);
            float maxVal = 0.00001f;
            foreach (var val in histSpan) if (val > maxVal) maxVal = val;

            float invMax = 255.0f / maxVal;
            for (int i = 0; i < histSpan.Length; i++)
            {
                result[i] = (byte)(histSpan[i] * invMax);
            }

            return new SpatialDensityDataByte
            {
                Position = original.Position.ToVector3(),
                MaxValue = maxVal,
                ByteDensities = result
            };
        }

        public static SpatialDensityData FromCompact(SpatialDensityDataByte compact, int binCount)
        {
            var directionsCount = Directions.Length;
            var hists = new float[directionsCount, binCount];
            var histSpan = MemoryMarshal.CreateSpan(ref hists[0, 0], hists.Length);

            float maxVal = compact.MaxValue;
            for (int i = 0; i < histSpan.Length; i++)
            {
                histSpan[i] = (compact.ByteDensities[i] / 255.0f) * maxVal;
            }

            return new SpatialDensityData
            {
                DensityMap = hists,
                Position = new Position(compact.Position)
            };
        }
    }
}
