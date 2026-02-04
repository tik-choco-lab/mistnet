using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MistNet.Utils
{
    public static class SphericalHistogramUtils
    {
        // デフォルト値 (Configから取得することを推奨)
        public const int DefaultDistBins = 4;
        
        // 方向ベクトルの定義
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
                Initialize(3); // デフォルトはLevel 3 (26方向)
                return _directions;
            }
        }

        public static void Initialize(int level)
        {
            List<Vector3> dirs = new List<Vector3>();
            switch (level)
            {
                case 1: // 6方向 (面)
                    dirs.AddRange(FaceDirections);
                    break;
                case 2: // 14方向 (面 + 頂点)
                    dirs.AddRange(FaceDirections);
                    dirs.AddRange(CornerDirections);
                    break;
                case 3: // 26方向 (面 + 頂点 + 辺)
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
                        // Level 4以上はフィボナッチ球体で均一分散
                        // 方向数は 26 * (level-2)^2 等でスケール
                        int count = 26 + (level - 3) * 50; 
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

            float phi = Mathf.PI * (3f - Mathf.Sqrt(5f)); // Golden angle

            for (int i = 0; i < count; i++)
            {
                float y = 1 - (i / (float)(count - 1)) * 2; // 1 to -1
                float radius = Mathf.Sqrt(1 - y * y);

                float theta = phi * i;

                float x = Mathf.Cos(theta) * radius;
                float z = Mathf.Sin(theta) * radius;

                directions[i] = new Vector3(x, y, z);
            }
            return directions;
        }

        public static float[,] CreateSphericalHistogram(Vector3 center, Vector3[] nodes, int distBins = DefaultDistBins)
        {
            return CreateSphericalHistogram(center, nodes, Directions, distBins);
        }

        public static float[,] CreateSphericalHistogram(Vector3 center, Vector3[] nodes, Vector3[] directions,
            int distBins = DefaultDistBins)
        {
            var dirCount = directions.Length;
            var hist = new float[dirCount, distBins];
            float maxDist = 0f;

            foreach (var node in nodes)
            {
                float dist = Vector3.Distance(center, node);
                if (dist > maxDist) maxDist = dist;
            }

            if (maxDist == 0f) maxDist = 1f;

            foreach (var node in nodes)
            {
                var vec = node - center;
                float dist = vec.magnitude;
                var unitVec = vec.normalized;

                int distIdx = Mathf.FloorToInt(dist / maxDist * (distBins - 1));

                for (int i = 0; i < dirCount; i++)
                {
                    float score = Vector3.Dot(directions[i], unitVec);
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

        public static float[,] ProjectSphericalHistogram(float[,] hist, Vector3 oldCenter, Vector3 newCenter,
            Vector3[] directions, int distBins = DefaultDistBins)
        {
            var offset = newCenter - oldCenter;
            float offsetNorm = offset.magnitude;
            if (offsetNorm == 0f) return (float[,])hist.Clone();

            var offsetUnit = offset.normalized;
            int dirCount = directions.Length;
            float[,] projected = (float[,])hist.Clone();

            for (int i = 0; i < dirCount; i++)
            {
                float cosSim = Mathf.Clamp(Vector3.Dot(directions[i], offsetUnit), -1f, 1f);
                for (int j = 0; j < distBins; j++)
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

        public static float[,] MergeHistograms(
            float[,] selfHist, Vector3 selfCenter,
            float[,] otherHist, Vector3 otherCenter,
            Vector3[] directions,
            int distBins = DefaultDistBins)
        {
            // 他人のヒストグラムを自分の中心に射影
            var otherProjected = ProjectSphericalHistogram(otherHist, otherCenter, selfCenter, directions, distBins);

            int dirs = directions.Length;
            var merged = new float[dirs, distBins];

            for (int i = 0; i < dirs; i++)
            {
                for (int j = 0; j < distBins; j++)
                {
                    merged[i, j] = selfHist[i, j] + otherProjected[i, j];
                }
            }

            return merged;
        }
    }
}
