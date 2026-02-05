using UnityEngine;

namespace MistNet.Test
{
    public class MemoryTest : MonoBehaviour
    {
        [ContextMenu("Test")]
        public void Test()
        {

        }

        private DNVEMessage CreateMessage()
        {
            var msg = new DNVEMessage
            {
                Type = DNVEMessageType.Heartbeat,
                Payload = ""
            };
            return msg;
        }
    }
}
