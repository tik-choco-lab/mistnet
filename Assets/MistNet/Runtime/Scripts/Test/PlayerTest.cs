using System;

namespace MistNet.Test
{
    public class PlayerTest : MistBehaviour
    {
        [MistSync(OnChanged = nameof(OnChanged))]
        private string PlayerName { get; set; }

        private void OnChanged()
        {
            MistDebug.Log($"[Test] Player name changed to: {PlayerName}");
        }

        protected override void LocalStart()
        {
            PlayerName = "Player_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            MistDebug.Log($"[Test] Player initialized with name: {PlayerName}");
        }
    }
}
