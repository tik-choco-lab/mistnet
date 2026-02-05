using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using MistNet.DNVE3;
using UnityEngine;
using MistNet;

namespace MistNet.Utils
{
    public static class SphericalHistogramUtils
    {
        public const int DefaultDistBins = 4;
        private const int Level1 = 1;
        private const int Level2 = 2;
        private const int Level3 = 3;
        private const int BaseDirectionCount = 26;
        private const int FibonacciStep = 50;

        private static readonly Vector3[] FaceDirections = new Vector3[]
        {
            new Vector3(0, 0, 1), new Vector3(0, 0, -1),
            new Vector3(1, 0, 0), new Vector3(-1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, -1, 0),
        };

        private static readonly Vector3[] CornerDirections = new Vector3[]
        {
            new Vector3(1, 1, 1), new Vector3(1, 1, -1),
            new Vector3(1, -1, 1), new Vector3(1, -1, -1),
            new Vector3(-1, 1, 1), new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1), new Vector3(-1, -1, -1)
        };

        private static readonly Vector3[] EdgeDirections = new Vector3[]
        {
            new Vector3(1, 1, 0), new Vector3(1, -1, 0),
            new Vector3(-1, 1, 0), new Vector3(-1, -1, 0),
            new Vector3(1, 0, 1), new Vector3(1, 0, -1),
            new Vector3(-1, 0, 1), new Vector3(-1, 0, -1),
            new Vector3(0, 1, 1), new Vector3(0, 1, -1),
            new Vector3(0, -1, 1), new Vector3(0, -1, -1),
        };

        private static Vector3[] _directions;
        public static Vector3[] Directions
        {
            get
            {
                if (_directions != null) return _directions;
                Initialize(Level3);
                return _directions;
            }
        }

        public static void Initialize(int level)
        {
            var dirs = new List<Vector3>();
            switch (level)
            {
                case Level1:
                    dirs.AddRange(FaceDirections);
                    break;
                case Level2:
                    dirs.AddRange(FaceDirections);
                    dirs.AddRange(CornerDirections);
                    break;
                case Level3:
                    dirs.AddRange(FaceDirections);
                    dirs.AddRange(CornerDirections);
                    dirs.AddRange(EdgeDirections);
                    break;
                default:
                    if (level <= 0)
                    {
                        dirs.AddRange(FaceDirections);
                    }
                    else
                    {
                        var count = BaseDirectionCount + (level - Level3) * FibonacciStep;
                        _directions = GenerateUniformDirections(count);
                        return;
                    }
                    break;
            }

            _directions = dirs.Select(d => d.normalized).ToArray();
        }

        public static Vector3[] GenerateUniformDirections(int count)
        {
            var directions = new Vector3[count];
            if (count <= 0) return directions;
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

        public static float[,] CreateSphericalHistogram(Vector3 center, Vector3[] nodes, int distBins = DefaultDistBins)
        {
            return CreateSphericalHistogram(center, nodes, Directions, distBins);
        }

        private static float[,] CreateSphericalHistogram(Vector3 center, Vector3[] nodes, Vector3[] directions,
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
                var distIdx = Mathf.FloorToInt(dist / maxDist * (distBins - 1));

                for (var i = 0; i < dirCount; i++)
                {
                    var score = Vector3.Dot(directions[i], unitVec);
                    if (score > 0f)
                        hist[i, distIdx] += score;
                }
            }

            return hist;
        }

        public static float[,] ProjectSphericalHistogram(float[,] hist, Vector3 oldCenter, Vector3 newCenter,
            int distBins = DefaultDistBins)
        {
            return ProjectSphericalHistogram(hist, oldCenter, newCenter, Directions, distBins);
        }

        private static float[,] ProjectSphericalHistogram(float[,] hist, Vector3 oldCenter, Vector3 newCenter,
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

        public static float[,] MergeHistograms(
            float[,] selfHist, Vector3 selfCenter,
            float[,] otherHist, Vector3 otherCenter,
            int distBins = DefaultDistBins)
        {
            return MergeHistograms(selfHist, selfCenter, otherHist, otherCenter, Directions, distBins);
        }

        private static float[,] MergeHistograms(
            float[,] selfHist, Vector3 selfCenter,
            float[,] otherHist, Vector3 otherCenter,
            Vector3[] directions,
            int distBins = DefaultDistBins)
        {
            var otherProjected = ProjectSphericalHistogram(otherHist, otherCenter, selfCenter, directions, distBins);
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

        public static SpatialHistogramDataByte ToCompact(SpatialHistogramData original)
        {
            var hists = original.Hists;
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

            return new SpatialHistogramDataByte
            {
                Position = original.Position.ToVector3(),
                MaxValue = maxVal,
                ByteHists = result
            };
        }

        public static SpatialHistogramData FromCompact(SpatialHistogramDataByte compact, int binCount)
        {
            var directionsCount = Directions.Length;
            var hists = new float[directionsCount, binCount];
            var histSpan = MemoryMarshal.CreateSpan(ref hists[0, 0], hists.Length);

            float maxVal = compact.MaxValue;
            for (int i = 0; i < histSpan.Length; i++)
            {
                histSpan[i] = (compact.ByteHists[i] / 255.0f) * maxVal;
            }

            return new SpatialHistogramData
            {
                Hists = hists,
                Position = new Position(compact.Position)
            };
        }
    }
}
