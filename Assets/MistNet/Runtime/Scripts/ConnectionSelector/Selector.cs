using UnityEngine;

namespace MistNet
{
    public class Selector : MonoBehaviour
    {
        [field:SerializeField] public SelectorBase SelectorBase { get; set; }
        [field:SerializeField] public RoutingBase RoutingBase { get; set; }
    }
}
