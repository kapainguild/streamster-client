
namespace DynamicStreamer.Queues
{
    public class ChangeTimeBaseQueue<T> : ITargetQueue<T> where T: IPayload
    {
        private readonly ITargetQueue<T> _output;
        private AVRational _from;
        private AVRational _to;

        public ChangeTimeBaseQueue(ITargetQueue<T> output, AVRational from, AVRational to)
        {
            _output = output;
            _from = from;
            _to = to;
        }

        public void Enqueue(Data<T> data)
        {
            data.Payload.RescaleTimebase(ref _from, ref _to);
            _output.Enqueue(data);
        }
    }
}
