using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Utils;

namespace MistNet.DNVE3
{
    public class DNVE3ConnectionBalancer : IDisposable
    {
        private readonly DNVE3DataStore _dnveDataStore;
        private readonly CancellationTokenSource _cts = new();

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        
        public DNVE3ConnectionBalancer(DNVE3DataStore dnveDataStore)
        {
            _dnveDataStore = dnveDataStore;
            LoopBalanceConnections(_cts.Token).Forget();
        }

        private async UniTask LoopBalanceConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.ConnectionBalancerIntervalSeconds),
                    cancellationToken: token);

                if (_dnveDataStore.SelfData == null) continue;
                if (_dnveDataStore.MergedHistogram == null) continue;
                var importantNodes= FindImportantNode();

                for (var i = 0; i < OptConfig.Data.ExchangeCount; i++)
                {
                    SendRequest(importantNodes[i].nodeId);
                }
            }
        }

        private void SendRequest(NodeId nodeId)
        {
            // MergedHistを送って、相手に足りなさそうなところを送ってもらう？
            // 欲しい方向ベクトルを1にして送る
            var hist = new float[SphericalHistogramUtils.Directions.Length, SphericalHistogramUtils.DistBins];
            for (var i = 0; i < SphericalHistogramUtils.Directions.Length; i++)
            {
                for (var j = 0; j < SphericalHistogramUtils.DistBins; j++)
                {
                    hist[i, j] = _dnveDataStore.MergedHistogram[i, j];
                }
            }
        }

        private List<(NodeId nodeId, float score)> FindImportantNode()
        {
            var otherNodeHists = _dnveDataStore.NodeMaps;
            var selfHist = _dnveDataStore.SelfData.Hists;
            var selfCenter = _dnveDataStore.SelfData.Position;

            var importantNodes = new List<(NodeId nodeId, float score)>();

            foreach (var (nodeId, nodeData) in otherNodeHists)
            {
                var otherHist = nodeData.Hists;
                var otherCenter = nodeData.Position;

                // 自分の中心に射影
                var projected = SphericalHistogramUtils.ProjectSphericalHistogram(
                    otherHist, otherCenter, selfCenter
                );

                var score = 0f;
                var distBins = SphericalHistogramUtils.DistBins;

                // 近距離ほど重くする
                for (var j = 0; j < distBins; j++)
                {
                    var weight = 1f / (1f + j); // distBin=0が最も重い

                    for (var i = 0; i < SphericalHistogramUtils.Directions.Length; i++)
                    {
                        // 他ノードの強度を評価
                        var diff = projected[i, j] - selfHist[i, j];
                        if (diff > 0f)
                            score += diff * weight;
                    }
                }

                if (score > 0f)
                    importantNodes.Add((nodeId, score));
            }

            // スコア順にソート
            importantNodes.Sort((a, b) => b.score.CompareTo(a.score));

            foreach (var (nodeId, score) in importantNodes)
                MistLogger.Debug($"[重要ノード] {nodeId} - 近距離強度スコア: {score:F3}");

            if (importantNodes.Count > 0)
                MistLogger.Debug($"最も近距離影響の強いノード: {importantNodes[0].nodeId}");

            return importantNodes;
        }
    }
}
