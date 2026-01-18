using System.Collections;
using MistNet.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SphericalHistogramTests
{
    private Vector3[] nodes;
    private Vector3 centerOld;
    private Vector3 centerNew;

    [SetUp]
    public void SetUp()
    {
        // ランダムノード
        nodes = new Vector3[500];
        var rng = new System.Random(42);
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i] = new Vector3(
                (float)(rng.NextDouble() * 10000),
                (float)(rng.NextDouble() * 10000),
                (float)(rng.NextDouble() * 10000)
            );
        }

        centerOld = Vector3.zero;
        centerNew = new Vector3(1, 1, 1);
    }

    [Test]
    public void TestHistogramProjectionShape()
    {
        var hist = SphericalHistogramUtils.CreateSphericalHistogram(centerOld, nodes);
        var histProj = SphericalHistogramUtils.ProjectSphericalHistogram(hist, centerOld, centerNew);

        Assert.AreEqual(26, hist.GetLength(0));
        Assert.AreEqual(SphericalHistogramUtils.DistBins, hist.GetLength(1));
        Assert.AreEqual(hist.GetLength(0), histProj.GetLength(0));
        Assert.AreEqual(hist.GetLength(1), histProj.GetLength(1));
    }

    [Test]
    public void TestProjectionNonNegative()
    {
        var hist = SphericalHistogramUtils.CreateSphericalHistogram(centerOld, nodes);
        var histProj = SphericalHistogramUtils.ProjectSphericalHistogram(hist, centerOld, centerNew);

        foreach (var val in histProj)
            Assert.GreaterOrEqual(val, 0f);
    }

    [Test]
    public void TestNoOffsetProjectionIdentity()
    {
        var hist = SphericalHistogramUtils.CreateSphericalHistogram(centerOld, nodes);
        var histProj = SphericalHistogramUtils.ProjectSphericalHistogram(hist, centerOld, centerOld);

        for (int i = 0; i < 26; i++)
            for (int j = 0; j < SphericalHistogramUtils.DistBins; j++)
                Assert.AreEqual(hist[i, j], histProj[i, j]);
    }

    [Test]
    public void TestProjectionAccuracy()
    {
        // 正解分布
        float[] distTrue = new float[26];
        foreach (var node in nodes)
        {
            Vector3 vec = node - centerNew;
            Vector3 unitVec = vec.magnitude > 0 ? vec.normalized : Vector3.zero;

            for (int i = 0; i < SphericalHistogramUtils.Directions.Length; i++)
                distTrue[i] += Mathf.Max(Vector3.Dot(unitVec, SphericalHistogramUtils.Directions[i]), 0f);
        }

        // 固定サイズヒストグラム
        float[,] hist = SphericalHistogramUtils.CreateSphericalHistogram(centerOld, nodes);
        float[,] histProj = SphericalHistogramUtils.ProjectSphericalHistogram(hist, centerOld, centerNew);

        // 近似分布を方向ごとに合計
        float[] distApprox = new float[26];
        for (int i = 0; i < 26; i++)
            for (int j = 0; j < SphericalHistogramUtils.DistBins; j++)
                distApprox[i] += histProj[i, j];

        // 誤差計算
        float sumSq = 0f;
        float maxErr = 0f;
        for (int i = 0; i < 26; i++)
        {
            float err = Mathf.Abs(distTrue[i] - distApprox[i]);
            sumSq += err * err;
            if (err > maxErr) maxErr = err;
        }
        float rmse = Mathf.Sqrt(sumSq / 26);

        Debug.Log($"ノード数: {nodes.Length}");
        Debug.Log($"ヒストグラムサイズ: {hist.Length}");
        Debug.Log($"正解分布: {string.Join(",", distTrue)}");
        Debug.Log($"近似分布: {string.Join(",", distApprox)}");
        Debug.Log($"RMSE: {rmse}");
        Debug.Log($"最大誤差: {maxErr}");

        float maxTrue = 0f;
        foreach (var v in distTrue) if (v > maxTrue) maxTrue = v;

        Assert.Less(rmse, maxTrue / 2, "RMSEが大きすぎる");
    }

    [UnityTest]
    public IEnumerator VectorDirectionWithEnumeratorPasses()
    {
        yield return null;
    }
}
