namespace MistNet
{
    public class MistLayer : ILayer
    {
        public IAOILayer AOI { get; set;  }
        public IWorldLayer World { get; set;  }
        public ITransportLayer Transport { get; set; }

        public void Init(IAOILayer aoiLayer, IWorldLayer worldLayer, ITransportLayer transportLayer)
        {
            AOI = aoiLayer;
            World = worldLayer;
            Transport = transportLayer;
        }
    }
}
