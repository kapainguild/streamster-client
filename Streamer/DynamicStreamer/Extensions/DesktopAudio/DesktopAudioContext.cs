using DynamicStreamer.Contexts;


namespace DynamicStreamer.Extensions.DesktopAudio
{
    public class DesktopAudioContext : IInputContext
    {
        public static string Name = "desktopaudio";

        private DesktopAudio _audio = new DesktopAudio();

        public InputConfig Config { get; private set; }

        public void Analyze(int duration, int streamsCount)
        {
        }

        public void Dispose()
        {
            _audio?.Dispose();
            _audio = null;
        }

        public void Interrupt()
        {
            _audio.Interrupt();
        }

        public void Open(InputSetup setup)
        {
            var format = _audio.Open(); 

            Config = new InputConfig(new[]
                {
                    new InputStreamProperties
                    {
                        CodecProps = new CodecProperties
                        {
                            codec_type = AVMediaType.AVMEDIA_TYPE_AUDIO,
                            codec_id = 65557, //AV_CODEC_ID_PCM_F32BE
                            sample_rate = format.SampleRate,
                            bits_per_coded_sample = 16,
                            bit_rate = 16 * format.SampleRate * format.Channels,
                            channels = format.Channels,
                            format = 3, // float, none-planar
                            extradata = new byte[1024]
                        }
                    }
                });
        }

        public void Read(Packet packet, InputSetup setup)
        {
            _audio.Read(packet);
        }
    }
}
