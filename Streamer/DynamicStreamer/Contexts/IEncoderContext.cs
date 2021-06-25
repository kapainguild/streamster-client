using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer
{
    public class EncoderSetup
    {
        public string Type { get; set; }

        public string Name { get; set; }

        public string Options { get; set; }

        public DirectXContext DirectXContext { get; set; }

        public bool SupportsEnforcingIFrame { get; set; }

        public EncoderSpec EncoderSpec;

        public EncoderBitrate EncoderBitrate;

        public override bool Equals(object obj)
        {
            return obj is EncoderSetup setup &&
                   Type == setup.Type &&
                   Name == setup.Name &&
                   DirectXContext == setup.DirectXContext &&
                   SupportsEnforcingIFrame == setup.SupportsEnforcingIFrame &&
                   Options == setup.Options &&
                   EncoderSpec.Equals(setup.EncoderSpec); //EncoderBitrate is missed conciusly
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Options, EncoderSpec, SupportsEnforcingIFrame);
        }

        public override string ToString()
        {
            return $"{Name} {Options}";
        }
    }

    public class EncoderConfig
    {
        public EncoderProperties EncoderProps = new EncoderProperties();

        public CodecProperties CodecProps = new CodecProperties();

        public override bool Equals(object obj)
        {
            return obj is EncoderConfig config &&
                   EncoderProps.Equals(config.EncoderProps) &&
                   CodecProps.Equals(config.CodecProps);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EncoderProps, CodecProps);
        }
    }

    public interface IEncoderContext : IDisposable
    {
        EncoderConfig Config { get; }

        int Open(EncoderSetup setup);

        int Write(Frame frame, bool enforceIFrame);

        ErrorCodes Read(Packet packet);

        void UpdateBitrate(EncoderBitrate encoderBitrate);
    }
}
