namespace MistNet
{
    public interface ILayer
    {
        IAOILayer AOI { get; set; }
        IWorldLayer World { get; set; }
        ITransportLayer Transport { get; set; }

        void Init(IAOILayer aoiLayer, IWorldLayer worldLayer, ITransportLayer transportLayer);
    }
}
