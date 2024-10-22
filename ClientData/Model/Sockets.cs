
namespace Streamster.ClientData.Model
{
    public interface ISocket
    {
        public SocketKind Kind { get; set; }

        public string Url { get; set; }
    }


    public enum SocketKind
    {
        PlatformWebSocket = 0,
    }
}
