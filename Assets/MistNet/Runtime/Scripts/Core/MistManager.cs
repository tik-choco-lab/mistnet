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
        public ILayer Layer { get; private set; }

        public void Awake()
        {
            MistConfig.ReadConfig();
            OptConfig.ReadConfig();

            Layer = new MistLayer();
            PeerRepository = new PeerRepository();
            _mistSyncManager = new MistSyncManager(Layer);
            I = this;

            Transport = new MistTransportLayer(Selector, PeerRepository, Layer);
            PeerRepository.Init(Transport);
            World = new MistWorldLayer(Transport, Selector, PeerRepository);
            AOI = new MistAOILayer(World, Selector, PeerRepository);
            Layer.Init(AOI, World, Transport);
            Selector.Init(PeerRepository, Layer);

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
