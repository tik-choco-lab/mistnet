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
        var nodeCount = 500;
        nodes = new Vector3[nodeCount];
        var rng = new System.Random(42);
        for (var i = 0; i < nodes.Length; i++)
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
        var densityMap = SpatialDensityUtils.CreateSpatialDensity(centerOld, nodes, nodes.Length);
        var densityMapProj = SpatialDensityUtils.ProjectSpatialDensity(densityMap, centerOld, centerNew);

        int dirCount = SpatialDensityUtils.Directions.Length;
        Assert.AreEqual(dirCount, densityMap.GetLength(0));
        Assert.AreEqual(SpatialDensityUtils.DefaultLayerCount, densityMap.GetLength(1));
        Assert.AreEqual(densityMap.GetLength(0), densityMapProj.GetLength(0));
        Assert.AreEqual(densityMap.GetLength(1), densityMapProj.GetLength(1));
    }

    [Test]
    public void TestProjectionNonNegative()
    {
        var densityMap = SpatialDensityUtils.CreateSpatialDensity(centerOld, nodes, nodes.Length);
        var densityMapProj = SpatialDensityUtils.ProjectSpatialDensity(densityMap, centerOld, centerNew);

        foreach (var val in densityMapProj)
            Assert.GreaterOrEqual(val, 0f);
    }

    [Test]
    public void TestNoOffsetProjectionIdentity()
    {
        var densityMap = SpatialDensityUtils.CreateSpatialDensity(centerOld, nodes, nodes.Length);
        var densityMapProj = SpatialDensityUtils.ProjectSpatialDensity(densityMap, centerOld, centerOld);

        var dirCount = densityMap.GetLength(0);
        var layerCount = densityMap.GetLength(1);
        for (var i = 0; i < dirCount; i++)
            for (var j = 0; j < layerCount; j++)
                Assert.AreEqual(densityMap[i, j], densityMapProj[i, j]);
    }

    [Test]
    public void TestProjectionAccuracy()
    {
        int dirCount = SpatialDensityUtils.Directions.Length;
        int layerCount = SpatialDensityUtils.DefaultLayerCount;

        // 正解分布
        var distTrue = new float[dirCount];
        foreach (var node in nodes)
        {
            var vec = node - centerNew;
            var unitVec = vec.magnitude > 0 ? vec.normalized : Vector3.zero;

            for (var i = 0; i < dirCount; i++)
                distTrue[i] += Mathf.Max(Vector3.Dot(unitVec, SpatialDensityUtils.Directions[i]), 0f);
        }

        // 固定サイズ空間密度
        var densityMap = SpatialDensityUtils.CreateSpatialDensity(centerOld, nodes, nodes.Length);
        var densityMapProj = SpatialDensityUtils.ProjectSpatialDensity(densityMap, centerOld, centerNew);

        // 近似分布を方向ごとに合計
        var distApprox = new float[dirCount];
        for (var i = 0; i < dirCount; i++)
            for (var j = 0; j < layerCount; j++)
                distApprox[i] += densityMapProj[i, j];

        // 誤差計算
        var sumSq = 0f;
        var maxErr = 0f;
        for (var i = 0; i < dirCount; i++)
        {
            var err = Mathf.Abs(distTrue[i] - distApprox[i]);
            sumSq += err * err;
            if (err > maxErr) maxErr = err;
        }
        var rmse = Mathf.Sqrt(sumSq / dirCount);

        Debug.Log($"ノード数: {nodes.Length}");
        Debug.Log($"空間密度データサイズ: {densityMap.Length}");
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
