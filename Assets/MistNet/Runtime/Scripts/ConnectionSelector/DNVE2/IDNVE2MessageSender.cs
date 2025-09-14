namespace MistNet.DNVE2
{
    public interface IDNVE2MessageSender
    {
        void Send(DNVE2Message message);
        void RegisterReceive(DNVE2MessageType type, DNVE2MessageReceivedHandler receiver);
    }

    public delegate void DNVE2MessageReceivedHandler(DNVE2Message message);
}
