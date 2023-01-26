using DynamicStreamer.Nodes;
using DynamicStreamer.Queues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicStreamer
{
	public class BitrateControllerEx<TConfig> : IBitrateController
	{
		private StreamerBase<TConfig> _clientStreamer;
		private readonly Func<TConfig, double, TConfig> _updater;
		private double _encoder_drc_ratio = 1.0;
		private string _trunkId;
		private int _configuredBitrate = 1000;
		private bool _initialized = false;
		private int _startBufferSize;
		private DateTime _startTime = DateTime.MinValue;

		private LinkedList<BitrateControllerMeasurement> _measurements = new LinkedList<BitrateControllerMeasurement>();

		public BitrateControllerEx(StreamerBase<TConfig> clientStreamer, Func<TConfig, double, TConfig> updater)
		{
			_clientStreamer = clientStreamer;
			_updater = updater;
		}

		public void InitOutput(OutputStreamQueueReader<Packet> reader)
		{
			ShutDown();

			reader.NotifyPacket = OnPacket;
		}

		private void OnPacket(Packet packet, int stream, int bufferSize)
		{
			bool iFrame = (packet.Properties.Flags & 1) > 0;
			bool video = stream == 0;
			DateTime time = DateTime.UtcNow;  
			if (video)
            {
				if (time != DateTime.MinValue)
                {
					var deltaTime = (time - _startTime).TotalSeconds;

					if (deltaTime > 1.0)
                    {

                    }


                }


				if (iFrame)
                {
					_startTime = time;
					_startBufferSize = bufferSize;
				}
            }
		}

		public void ShutDown()
		{
		}

		public void Reconnected()
		{
			if (_encoder_drc_ratio != 1.0)
			{
				_encoder_drc_ratio = 1.0;
				_clientStreamer.TuneConfig(c => _updater(c, _encoder_drc_ratio));
			}
			_initialized = false;
		}

		internal void SetId(string id, int bitrate)
		{
			_trunkId = id;
			_configuredBitrate = bitrate;
		}
	}
}
