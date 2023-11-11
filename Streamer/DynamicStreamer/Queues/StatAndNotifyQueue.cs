using System;

namespace DynamicStreamer.Queues
{
    public class StatAndNotifyQueue : ITargetQueue<Packet>
    {
        private readonly ITargetQueue<Packet> _output;

        public int GopSize { get; set; }

        public int PacketNumber { get; set; }

        public int CurrentBitrate { get; set; }

        public Action NotifyIFrame { get; set; }

        public Action NotifyBitrate { get; set; }

        public int BeforeIFrameNotify { get; set; }

        private DateTime _startMeasurements = DateTime.MinValue;
        private int[] _bytes = new int[4];
        private int _position = 0;
        private int _lastNotifiedBitrate = 0;

        public StatAndNotifyQueue(ITargetQueue<Packet> output)
        {
            _output = output;
        }

        public void Enqueue(Data<Packet> data)
        {
            if (data.SourceId == 0) // video only
            {
                bool iFrame = ((data.Payload.Properties.Flags & Core.FLAG_IFRAME) > 0);

                if (iFrame)
                {
                    if (PacketNumber != 0)
                        GopSize = PacketNumber;
                    PacketNumber = 0;
                }

                PacketNumber++;

                if (PacketNumber == GopSize - BeforeIFrameNotify)
                {
                    NotifyIFrame?.Invoke();
                }
            }

            DoStatistics(data.Payload.Properties.Size);

            _output.Enqueue(data);
        }

        private void DoStatistics(int size)
        {
            var dateTime = DateTime.UtcNow;
            
            if (_startMeasurements == DateTime.MinValue)
            {
                _startMeasurements = dateTime;
                _bytes[0] += size;
            }
            else
            {
                int newPosition = (int)(dateTime - _startMeasurements).TotalSeconds;

                if (newPosition > _position)
                {
                    for (int q = _position + 1; q < newPosition; q++)
                        _bytes[q % _bytes.Length] = 0;

                    int count = Math.Min(newPosition, _bytes.Length);
                    int sum = 0;
                    for (int q = 0; q < count; q++)
                        sum += _bytes[q];

                    sum = sum * 8 / count / 1024;

                    CurrentBitrate = sum;
                    if (!(_lastNotifiedBitrate*0.9 < sum && sum < 1.1*_lastNotifiedBitrate)) // not within +/- 10%
                    {
                        Core.LogInfo($"Incoming bitrate: {sum}");
                        NotifyBitrate?.Invoke();
                        _lastNotifiedBitrate = sum;
                    }

                    _position = newPosition;
                    _bytes[_position % _bytes.Length] = size;
                }
                else
                    _bytes[_position % _bytes.Length] += size;
            }
        }
    }
}
