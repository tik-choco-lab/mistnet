using System;
using UnityEngine;

namespace MistNet.Test
{
    public class PlayerTest : MistBehaviour
    {
        [MistSync(OnChanged = nameof(OnChanged))]
        private string playerName { get; set; }

        private void OnChanged()
        {
            MistDebug.Log($"[Test] Player name changed to: {playerName}");
        }

        protected override void LocalStart()
        {
            playerName = "Player_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            MistDebug.Log($"[Test] Player initialized with name: {playerName}");
        }
    }
}
