using System.Threading;
using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEngine;
using System;

namespace MistNet
{
    public class MistStats : MonoBehaviour
    {
        public static MistStats I { get; private set; }
        private static readonly float IntervalPingDistanceTimeSec = 1f;
        private static readonly int IntervalSendSizeTimeSec = 1;
        
        public int TotalSendBytes { get; set; }
        public int TotalReceiveBytes { get; set; }
        public int TotalMessageCount { get; set; }
        
        private CancellationTokenSource _cancellationToken;
        public EvalStatData StatData { get; private set; } = new EvalStatData();

        private void Start()
        {
            I = this;
            MistManager.I.AddRPC(MistNetMessageType.Ping, ReceivePing);
            MistManager.I.AddRPC(MistNetMessageType.Pong, ReceivePong);
            _cancellationToken = new CancellationTokenSource();
            UpdatePing(_cancellationToken.Token).Forget();
            UpdateSendSize(_cancellationToken.Token).Forget();
        }
        
        private void OnDestroy()
        {
            _cancellationToken.Cancel();
        }

        private void ReceivePing(byte[] data, NodeId sourceId)
        {
            var ping = MemoryPackSerializer.Deserialize<P_Ping>(data);
            var pong = new P_Pong
            {
                Time = ping.Time,
            };
            var sendData = MemoryPackSerializer.Serialize(pong);
            MistManager.I.Send(MistNetMessageType.Pong, sendData ,sourceId);
        }
        
        private void ReceivePong(byte[] data, NodeId sourceId)
        {
            var pong = MemoryPackSerializer.Deserialize<P_Pong>(data);
            var time = DateTime.Now.Ticks - pong.Time;
            var timeSpan = new TimeSpan(time);
            var rtt = (int)timeSpan.TotalMilliseconds;
            MistLogger.Debug($"[STATS][RTT][{sourceId}] {rtt} ms");

            StatData.RttMillis[sourceId] = rtt;
        }

        private async UniTask UpdatePing(CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                var ping = new P_Ping
                {
                    Time = DateTime.Now.Ticks,
                };
                var sendData = MemoryPackSerializer.Serialize(ping);
                MistManager.I.SendAll(MistNetMessageType.Ping, sendData);
                
                await UniTask.Delay(TimeSpan.FromSeconds(IntervalPingDistanceTimeSec), cancellationToken: token);
            }
        }
        
        private async UniTask UpdateSendSize(CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                // 現在の接続人数を調べる
                var peers = MistManager.I.Routing.ConnectedNodes;
                MistLogger.Debug($"[STATS][Peers] {peers.Count}");
                
                // 帯域幅(bps)を計算
                var sendBps = TotalSendBytes * 8 / IntervalSendSizeTimeSec;
                MistLogger.Debug($"[STATS][Upload]\t\t{FormatBps(sendBps)}\t{sendBps} bps");
                
                var receiveBps = TotalReceiveBytes * 8 / IntervalSendSizeTimeSec;
                MistLogger.Debug($"[STATS][Download]\t{FormatBps(receiveBps)}\t{receiveBps} bps");
                
                // メッセージ数
                MistLogger.Debug($"[STATS][MessageCount] {TotalMessageCount}");

                StatData.SendBits = sendBps;
                StatData.ReceiveBits = receiveBps;
                StatData.MessageCount = TotalMessageCount;
                
                TotalSendBytes = 0;
                TotalReceiveBytes = 0;
                TotalMessageCount = 0;
                
                await UniTask.Delay(TimeSpan.FromSeconds(IntervalSendSizeTimeSec), cancellationToken: token);
            }
        }

        private string FormatBps(float bps)
        {
            string unit;
            double scaled;

            if (bps >= 1_000_000)
            {
                scaled = bps / 1_000_000;
                unit = "Mbps";
            }
            else if (bps >= 1_000)
            {
                scaled = bps / 1_000;
                unit = "Kbps";
            }
            else
            {
                scaled = bps;
                unit = "bps";
            }

            // 右揃え幅10、少数2桁
            return $"{scaled,10:F2} {unit}";
        }
    }
}
