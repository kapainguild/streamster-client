using DynamicStreamer.Queues;
using System;

namespace DynamicStreamer
{
    public interface IBitrateController
    {
        void InitOutput(OutputStreamQueueReader<Packet> reader);
        void Reconnected();
    }

    public class BitrateController<TConfig> : IBitrateController
    {
        private StreamerBase<TConfig> _clientStreamer;
        private readonly Func<TConfig, double, TConfig> _updater;
        private OutputStreamQueueReader<Packet> _reader;
        private TimerSubscription _timerSubscription = null;
        private int encoder_drc_buffer_size;
        private int encoder_drc_positive_counter;
        private double encoder_drc_ratio;

        public BitrateController(StreamerBase<TConfig> clientStreamer, Func<TConfig, double, TConfig> updater)
        {
            _clientStreamer = clientStreamer;
            _updater = updater;
        }

        public void InitOutput(OutputStreamQueueReader<Packet> reader)
        {
            ShutDown();

            _reader = reader;
            _timerSubscription = _clientStreamer.Subscribe(990, OnTimer);
        }

        private void OnTimer()
        {
			int buffer = _reader.BufferSize;

			int delta = buffer - encoder_drc_buffer_size;
			encoder_drc_buffer_size = buffer;
			double ratio = -1;

			if (delta > 3 && buffer > 45 || buffer > 90)
			{
				encoder_drc_positive_counter = 0;
				if (delta < 20 && buffer < 120 || delta < 0)
				{
					ratio = encoder_drc_ratio * .95;
					Core.LogInfo($"--- slow down {ratio:F2} (d{delta}, b{buffer})");
				}
				else
				{
					ratio = encoder_drc_ratio * .85;
					Core.LogInfo($"------ fast down {ratio:F2} (d{delta}, b{buffer})");
				}
			}

			if (delta < 5 || buffer < 15)
			{
				encoder_drc_positive_counter++;
				if (encoder_drc_positive_counter > 2)
				{
					encoder_drc_positive_counter = 0;

					if (encoder_drc_ratio < 1.0)
					{
						ratio = encoder_drc_ratio * 1.07;
						if (ratio > 1.0)
							ratio = 1.0;
						Core.LogInfo($"+++ up {ratio:F2} (d{delta}, b{buffer})");

					}
				}
			}

			if (ratio > 0.15 && ratio <= 1.0)
			{
				encoder_drc_ratio = ratio;

				_clientStreamer.TuneConfig(c => _updater(c, ratio));
			}
		}

        public void ShutDown()
        {
            if (_timerSubscription != null)
            {
                _clientStreamer.UnsubscribeTimer(_timerSubscription);
                _timerSubscription = null;
				Reconnected();
			}
            _reader = null;
			
		}

        public void Reconnected()
        {
			if (encoder_drc_ratio != 1.0)
            {
				encoder_drc_ratio = 1.0;
				_clientStreamer.TuneConfig(c => _updater(c, encoder_drc_ratio));
			}
			
			encoder_drc_buffer_size = 0;
			encoder_drc_positive_counter = 0;
		}
    }
}
