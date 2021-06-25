using System;

namespace DynamicStreamer.Extension
{
    public interface IExtensionLogger
    {
        void Info(string message, string template = null);
        void Warining(string message, string template = null);
        void Error(Exception e, string message);
    }
}
