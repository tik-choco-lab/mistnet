using UnityEngine;

namespace MistNet
{
    public class MistAnimatorState : MonoBehaviour
    {
        private MistSyncObject SyncObject { get; set; }
        [field: SerializeField] public Animator Animator { get; set; }
        private bool _isDirty;

        private int _previousStateHash = -1;

        private void Awake()
        {
            SyncObject = GetComponentInParent<MistSyncObject>();
        }

        private void Update()
        {
            if (SyncObject.IsOwner)
            {
                LocalUpdate();
            }
        }

        private void LocalUpdate()
        {
            if (Animator == null) return;

            var layerCount = Animator.layerCount;

            for (var i = 0; i < layerCount; i++)
            {
                var stateInfo = Animator.GetCurrentAnimatorStateInfo(i);

                var currentStateHash = stateInfo.shortNameHash;
                if (_previousStateHash == currentStateHash) continue;

                _previousStateHash = currentStateHash;
                SyncObject.RPCOther(nameof(RPC_PlayAnimation), currentStateHash, i);
            }
        }

        [MistRpc]
        private void RPC_PlayAnimation(int animationHash, int layer = 0)
        {
            if (Animator == null) return;
            var currentState = Animator.GetCurrentAnimatorStateInfo(layer);
            if (currentState.shortNameHash == animationHash) return;

            Animator.Play(animationHash, layer, 0);
        }
    }
}
