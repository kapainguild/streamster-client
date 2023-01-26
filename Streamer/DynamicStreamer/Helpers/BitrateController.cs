using DynamicStreamer.Nodes;
using DynamicStreamer.Queues;
using System;
using System.Collections.Generic;
using System.Linq;

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
        private double encoder_drc_ratio = 1.0;
        private string _trunkId;
		private int _configuredBitrate = 1000;
		private LinkedList<BitrateControllerMeasurement> _measurements = new LinkedList<BitrateControllerMeasurement>();
        //private int _bufferSize;

        public BitrateController(StreamerBase<TConfig> clientStreamer, Func<TConfig, double, TConfig> updater)
        {
            _clientStreamer = clientStreamer;
            _updater = updater;
        }

        public void InitOutput(OutputStreamQueueReader<Packet> reader)
        {
            ShutDown();

            _reader = reader;
			//reader.NotifyPacket = OnPacket;
            _timerSubscription = _clientStreamer.Subscribe(990, OnTimer);
        }

		//private void OnPacket(Packet packet, int stream, int bufferSize)
		//{
		//	_bufferSize = bufferSize;
		//}

        private void OnTimer()
        {
			int buffer = _reader.BufferSize; //_bufferSize;
			int delta = buffer - encoder_drc_buffer_size;

			var stat = _clientStreamer.ResourceManager.GetStatistics(out var period);
			var bitrateStat = stat.FirstOrDefault(s => s.Name?.Trunk == _trunkId);
			var bitrate = (bitrateStat?.Data as StatisticDataOfInputOutput)?.Bytes;
			if (bitrate.HasValue && bitrateStat.DurationMs > 700)
            {
				var durationRate = bitrateStat.DurationMs / 1000;
				int bt = (int) (bitrate.Value / 128 / durationRate); // 128 = 1024 / 8


				bool ok = delta < 5 || buffer < 15;
				_measurements.AddLast(new BitrateControllerMeasurement(bt, ok));
				if (_measurements.Count > 120)
					_measurements.RemoveFirst();
			}


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
						var maxOkBitRate = GetMaxOkBitrate();

						if (maxOkBitRate > 0)
						{
							ratio = maxOkBitRate;
							Core.LogInfo($"+++ FAST up {ratio:F2} (d{delta}, b{buffer})");
						}
						else
						{
							ratio = encoder_drc_ratio * 1.07;
							if (ratio > 1.0)
								ratio = 1.0;
							Core.LogInfo($"+++ up {ratio:F2} (d{delta}, b{buffer})");
						}
					}
				}
			}

			if (ratio > 0.15 && ratio <= 1.0)
			{
				encoder_drc_ratio = ratio;

				_clientStreamer.TuneConfig(c => _updater(c, ratio));
			}
		}

        private double GetMaxOkBitrate()
        {
			var sortedOk = _measurements.Where(s => s.Ok).OrderByDescending(s => s.Bitrate).ToList();
			if (sortedOk.Count > 10)
            {
				int max = sortedOk[sortedOk.Count/2].Bitrate; // take middle/mean value
				var maxRatio = (double)max / _configuredBitrate;
				if (maxRatio > 1.0)
					return 1.0;
				else
                {
					var bottom = maxRatio / 1.07;
					if (bottom > encoder_drc_ratio)
						return bottom;
                }
			}
			return -1;
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

        internal void SetId(string id, int bitrate)
        {
			_trunkId = id;
			_configuredBitrate = bitrate;
		}
    }

	record BitrateControllerMeasurement(int Bitrate, bool Ok);
}
