using System;

namespace MistNet.Test
{
    public class PlayerTest : MistBehaviour
    {
        [MistSync(OnChanged = nameof(OnChanged))]
        private string PlayerName { get; set; }
        [MistSync(OnChanged = nameof(OnChanged2))]
        private string PlayerName2 { get; set; }
        [MistSync(OnChanged = nameof(OnChanged3))]
        private string PlayerName3 { get; set; }
        [MistSync(OnChanged = nameof(OnChanged4))]
        private string PlayerName4 { get; set; }

        private void OnChanged()
        {
            MistLogger.Debug($"[Test] Player name changed to: {PlayerName}");
        }

        private void OnChanged2()
        {
            MistLogger.Debug($"[Test] Player name 2 changed to: {PlayerName2}");
        }

        private void OnChanged3()
        {
            MistLogger.Debug($"[Test] Player name 3 changed to: {PlayerName3}");
        }

        private void OnChanged4()
        {
            MistLogger.Debug($"[Test] Player name 4 changed to: {PlayerName4}");
        }

        protected override void LocalStart()
        {
            PlayerName = "Player_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            PlayerName2 = "Player2_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            PlayerName3 = "Player3_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            PlayerName4 = "Player4_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            MistLogger.Debug($"[Test] Player initialized with name: {PlayerName}");
        }
    }
}
