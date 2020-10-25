namespace Streamster.ClientData
{
    public class LoadBalancerResponse
    {
        public string Server { get; set; }

        public ClientVersion[] UpperVersions { get; set; }
    }
}
