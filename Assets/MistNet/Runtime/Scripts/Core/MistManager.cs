using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet
{
    /// <summary>
    /// Messageの管理を行う
    /// </summary>
    public class MistManager : MonoBehaviour
    {
        public static MistManager I;
        public IPeerRepository PeerRepository;

        [field: SerializeField] public Selector Selector { get; private set; }
        public MistSignalingWebSocket MistSignalingWebSocket { get; private set; }

        private MistSyncManager _mistSyncManager;
        public RoutingBase Routing => Selector.RoutingBase;

        public IAOILayer AOI { get; private set; }
        public IWorldLayer World { get; private set; }
        public ITransportLayer Transport { get; private set; }

        public void Awake()
        {
            MistConfig.ReadConfig();
            PeerRepository = new PeerRepository();
            _mistSyncManager = new MistSyncManager();
            PeerRepository.Init();
            I = this;

            Transport = new MistTransportLayer(Selector, PeerRepository);
            World = new MistWorldLayer(Transport, Selector);
            AOI = new MistAOILayer(World, Selector);

            Transport.Init();
        }

        private void Start()
        {
            MistSignalingWebSocket = new MistSignalingWebSocket(PeerRepository);
            MistSignalingWebSocket.Init().Forget();
            _mistSyncManager.Start();
        }

        public void OnDestroy()
        {
            PeerRepository.Dispose();
            _mistSyncManager.Dispose();
            MistSignalingWebSocket.Dispose();
            Transport.Dispose();
            World.Dispose();
            AOI.Dispose();
        }

        private void Update()
        {
            _mistSyncManager.UpdateSyncObjects();
        }
    }
}
