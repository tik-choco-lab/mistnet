using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MistNet.DNVE2
{
    public class DNVE2ConnectionBalancer : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();

        public DNVE2ConnectionBalancer(IDNVE2MessageSender messageSender)
        {
            messageSender.RegisterReceive(DNVE2MessageType.NodeList, OnNodeListReceived);
            LoopBalanceConnections(_cts.Token).Forget();
        }

        private void OnNodeListReceived(DNVE2Message message)
        {
            MistLogger.Debug($"[DNVE2ConnectionBalancer] OnNodeListReceived: {message.Payload} from {message.Sender}");
            // Handle the node list message
        }

        private async UniTask LoopBalanceConnections(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.ConnectionBalancerIntervalSeconds),
                    cancellationToken: token);

                SelectConnection();
                SelectDisconnection();
                SendLocation();
            }
        }

        private void SelectConnection()
        {
            // Implement connection selection logic
        }

        private void SelectDisconnection()
        {
            // Implement disconnection selection logic
        }

        private void SendLocation()
        {
            // Implement location sending logic
    }

        public void Dispose()
        {
            _cts.Cancel();
            _cts?.Dispose();
        }
    }
}
