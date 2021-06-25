using DynamicStreamer.Extension;
using System;

namespace DynamicStreamer
{
    class ExtensionLogger : IExtensionLogger
    {
        private readonly string _prefix;

        public ExtensionLogger(string prefix)
        {
            _prefix = prefix;
        }

        public void Error(Exception e, string message)
        {
            Core.LogError(e, _prefix + message);
        }

        public void Info(string message, string template = null)
        {
            Core.LogInfo(_prefix + message, template);
        }

        public void Warining(string message, string template = null)
        {
            Core.LogWarning(_prefix + message, template);
        }
    }
}
