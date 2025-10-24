using System;
using System.Collections.Generic;

namespace MistNet.DNVE3
{
    public class DNVE3Exchanger : IDisposable
    {
        private readonly IMessageSender _sender;

        public void Dispose()
        {

        }

        public DNVE3Exchanger(IMessageSender sender)
        {
            _sender = sender;
            _sender.RegisterReceive(DNVEMessageType.Heartbeat, OnHeartbeatReceived);
        }

        private void OnHeartbeatReceived(DNVEMessage message)
        {

        }

        private DirectionalFeatureData GetDirectionalFeatureData(List<Node> nodes)
        {


            return new DirectionalFeatureData
            {
                FeatureValues = new float[] { 0.1f, 0.2f, 0.3f }
            };
        }
    }
}
