using System.Collections.Generic;
using UnityEngine;

namespace MistNet
{
    public class MistAnimatorState : MonoBehaviour
    {
        private MistSyncObject SyncObject { get; set; }
        [field: SerializeField] public Animator Animator { get; set; }
        private bool _isDirty;

        private int _previousStateHash = -1;
        private readonly List<float> _currentTime = new();
        private readonly List<float> _previousTime = new();

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
            if (_currentTime.Count != layerCount) InitTimeDict(layerCount);

            for (var layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                var stateInfo = Animator.GetCurrentAnimatorStateInfo(layerIndex);

                if (IsSameAnimation(ref stateInfo, layerIndex)) continue;

                var currentStateHash = stateInfo.shortNameHash;
                _previousStateHash = currentStateHash;
                _previousTime[layerIndex] = Animator.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime;
                SyncObject.RPCOther(nameof(RPC_PlayAnimation), currentStateHash, layerIndex, stateInfo.normalizedTime);
            }
        }

        private bool IsSameAnimation(ref AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (stateInfo.shortNameHash != _previousStateHash) return false;

            var normalizedTime = stateInfo.normalizedTime;
            var isReplay = normalizedTime < _previousTime[layerIndex]; // 再度再生されたか
            return !isReplay;
        }

        private void InitTimeDict(int layerCount)
        {
            _currentTime.Clear();
            _previousTime.Clear();
            for (var i = 0; i < layerCount; i++)
            {
                _currentTime.Add(0);
                _previousTime.Add(0);
            }
        }

        [MistRpc]
        private void RPC_PlayAnimation(int animationHash, int layer = 0, float normalizedTime = 0)
        {
            if (Animator == null) return;

            Animator.Play(animationHash, layer, normalizedTime);
        }
    }
}
