using UnityEngine;

namespace MistNet
{
    public abstract class IRouting : MonoBehaviour
    {
        public virtual void Add(string sourceId, string fromId)
        {
        }

        public virtual string Get(string targetId)
        {
            return null;
        }
    }
}
