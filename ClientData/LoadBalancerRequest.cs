
namespace ClientData
{
    public class LoadBalancerRequest
    {
        /// <summary>
        /// To change server
        /// </summary>
        public string TryExclude { get; set; }

        /// <summary>
        /// Incremental number of request
        /// </summary>
        public int RequestCounter { get; set; }
    }
}
