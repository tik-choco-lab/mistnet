using UnityEngine;

namespace MistNet.Utils
{
    public static class SphericalHistogramUtils
    {
        // 26方向ベクトルを定数化
        public static readonly Vector3[] Directions = new Vector3[]
        {
            new Vector3(0, 0, 1), new Vector3(0, 0, -1),
            new Vector3(1, 0, 0), new Vector3(-1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, -1, 0),
            new Vector3(1, 1, 0), new Vector3(1, -1, 0),
            new Vector3(-1, 1, 0), new Vector3(-1, -1, 0),
            new Vector3(1, 0, 1), new Vector3(1, 0, -1),
            new Vector3(-1, 0, 1), new Vector3(-1, 0, -1),
            new Vector3(0, 1, 1), new Vector3(0, 1, -1),
            new Vector3(0, -1, 1), new Vector3(0, -1, -1),
            new Vector3(1, 1, 1), new Vector3(1, 1, -1),
            new Vector3(1, -1, 1), new Vector3(1, -1, -1),
            new Vector3(-1, 1, 1), new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1), new Vector3(-1, -1, -1)
        };

        static SphericalHistogramUtils()
        {
            // 正規化
            for (int i = 0; i < Directions.Length; i++)
                Directions[i] = Directions[i].normalized;
        }

        public static float[,] CreateSphericalHistogram(Vector3 center, Vector3[] nodes, int distBins)
        {
            return CreateSphericalHistogram(center, nodes, Directions, distBins);
        }

        public static float[,] CreateSphericalHistogram(Vector3 center, Vector3[] nodes, Vector3[] directions,
            int distBins)
        {
            var hist = new float[26, distBins];
            float maxDist = 0f;

            foreach (var node in nodes)
            {
                float dist = Vector3.Distance(center, node);
                if (dist > maxDist) maxDist = dist;
            }

            if (maxDist == 0f) maxDist = 1f;

            foreach (var node in nodes)
            {
                Vector3 vec = node - center;
                float dist = vec.magnitude;
                Vector3 unitVec = vec.normalized;

                int distIdx = Mathf.FloorToInt(dist / maxDist * (distBins - 1));

                for (int i = 0; i < directions.Length; i++)
                    hist[i, distIdx] += Mathf.Max(Vector3.Dot(directions[i], unitVec), 0f);
            }

            return hist;
        }

        public static float[,] ProjectSphericalHistogram(float[,] hist, Vector3 oldCenter, Vector3 newCenter,
            int distBins)
        {
            return ProjectSphericalHistogram(hist, oldCenter, newCenter, Directions, distBins);
        }

        public static float[,] ProjectSphericalHistogram(float[,] hist, Vector3 oldCenter, Vector3 newCenter,
            Vector3[] directions, int distBins)
        {
            Vector3 offset = newCenter - oldCenter;
            float offsetNorm = offset.magnitude;
            if (offsetNorm == 0f) return (float[,])hist.Clone();

            Vector3 offsetUnit = offset.normalized;
            float[,] projected = (float[,])hist.Clone();

            for (int i = 0; i < directions.Length; i++)
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
    }
}
