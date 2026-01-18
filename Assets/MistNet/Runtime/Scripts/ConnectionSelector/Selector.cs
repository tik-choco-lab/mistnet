using UnityEngine;

namespace MistNet
{
    public class Selector : MonoBehaviour
    {
        [field:SerializeField] public SelectorBase SelectorBase { get; set; }
        [field:SerializeField] public RoutingBase RoutingBase { get; set; }

        public void Init(IPeerRepository peerRepository, ILayer layer)
        {
            SelectorBase.Init(peerRepository, layer);
            RoutingBase.Init(peerRepository, layer);
            SelectorBase.SetRoutingBase(RoutingBase);
        }
    }
}
