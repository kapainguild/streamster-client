using DynamicStreamer.Contexts;
using System;

namespace DynamicStreamer
{

    public class DecoderSetup
    {
        public string Type { get; set; }

        public CodecProperties CodecProps { get; set; }

        public DirectXContext DirectXContext { get; set; }

        public DecoderSetup(string type, CodecProperties codecProps, DirectXContext directXContext)
        {
            Type = type;
            CodecProps = codecProps;
            DirectXContext = directXContext;
        }

        public override bool Equals(object obj)
        {
            return obj is DecoderSetup setup &&
                    Type == setup.Type &&
                    DirectXContext == setup.DirectXContext &&
                    CodecProps.Equals(setup.CodecProps);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CodecProps, Type, DirectXContext);
        }

        public override string ToString()
        {
            if (Type == DecoderContextDirectXPassThru.Type)
            {
                if (CodecProps.format == Core.Const.PIX_FMT_BGRA)
                    return $"{CodecProps} =pass=> dx";
                else
                    return $"dx({CodecProps}) =pass=> dx";
            }
            if (Type == DecoderContextDirectXUpload.Type)
                return $"{CodecProps} => dx";

            return $"{CodecProps} => frame";
        }
    }

    public class DecoderConfig
    {
        public CodecProperties CodecProperties;
        public DecoderProperties DecoderProperties;

        public override bool Equals(object obj)
        {
            return obj is DecoderConfig config &&
                   CodecProperties.Equals(config.CodecProperties) &&
                   DecoderProperties.Equals(config.DecoderProperties);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CodecProperties, DecoderProperties);
        }
    }

    public interface IDecoderContext : IDisposable
    {
        DecoderConfig Config { get; }

        int Open(DecoderSetup setup);

        int Write(Packet packet);

        ErrorCodes Read(Frame frame);
    }
}
