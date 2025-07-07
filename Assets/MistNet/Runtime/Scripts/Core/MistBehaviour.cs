using UnityEngine;

namespace MistNet
{
    [RequireComponent(typeof(MistSyncObject))]
    public class MistBehaviour : MonoBehaviour
    {
        protected MistSyncObject SyncObject { get; private set; }

        protected virtual void Awake()
        {
            SyncObject = transform.root.GetComponent<MistSyncObject>();

            if (SyncObject.IsOwner)
            {
                LocalAwake();
            }
            else
            {
                RemoteAwake();
            }
        }

        protected virtual void Start()
        {
            if (SyncObject.IsOwner)
            {
                LocalStart();
            }
            else
            {
                RemoteStart();
            }
        }

        protected virtual void Update()
        {
            if (SyncObject.IsOwner)
            {
                LocalUpdate();
            }
            else
            {
                RemoteUpdate();
            }
        }

        protected virtual void FixedUpdate()
        {
            if (SyncObject.IsOwner)
            {
                LocalFixedUpdate();
            }
            else
            {
                RemoteFixedUpdate();
            }
        }

        protected virtual void OnDestroy()
        {
            if (SyncObject.IsOwner)
            {
                LocalOnDestroy();
            }
            else
            {
                RemoteOnDestroy();
            }
        }

        protected virtual void LocalAwake()
        {
        }

        protected virtual void LocalStart()
        {
        }

        protected virtual void LocalUpdate()
        {
        }

        protected virtual void LocalFixedUpdate()
        {
        }

        protected virtual void LocalOnDestroy()
        {
        }


        protected virtual void RemoteAwake()
        {
        }

        protected virtual void RemoteStart()
        {
        }

        protected virtual void RemoteUpdate()
        {
        }

        protected virtual void RemoteFixedUpdate()
        {
        }

        protected virtual void RemoteOnDestroy()
        {
        }
    }
}
