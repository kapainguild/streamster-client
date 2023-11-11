
namespace DynamicStreamer.Queues
{
    public class SetFlagQueue<T> : ITargetQueue<T> where T : IPayload
    {
        private readonly ITargetQueue<T> _output;
        private readonly int _flag;

        public SetFlagQueue(ITargetQueue<T> output, int flag)
        {
            _output = output;
            _flag = flag;
        }

        public void Enqueue(Data<T> data)
        {
            data.Payload.SetFlag(_flag);
            _output.Enqueue(data);
        }
    }
}
