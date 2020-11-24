using Serilog;
using System;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class TransientMessageModel
    {
        private int _currentMessageId;

        public TransientMessageModel()
        {
            Close = () => Clear();
        }

        public Property<TransientMessageType> Type { get; } = new Property<TransientMessageType>(TransientMessageType.None);

        public Property<string> Message { get; } = new Property<string>();

        public Action Close { get; }

        public int Show(string message, TransientMessageType type, bool timeOut = true, int messageToOverride = -1)
        {
            if (type == TransientMessageType.Error)
                Log.Warning($"UI source error '{message}'");
            else
                Log.Information($"UI source info '{message}'");

            int currentMessageId;
            lock(this)
            {
                if (messageToOverride != -1 && messageToOverride != _currentMessageId)
                    return messageToOverride;
                currentMessageId = ++_currentMessageId;

                Message.Value = message;
                Type.Value = type;
            }

            if (timeOut)
            {
                _ = Resetter();

                async Task Resetter()
                {
                    await Task.Delay(7_000);
                    Clear(currentMessageId);
                }
            }
            return currentMessageId;
        }

        public void Clear(int id = -1)
        {
            lock(this)
            {
                if (id == -1 || id == _currentMessageId)
                    Type.Value = TransientMessageType.None;
            }
        }
    }

    public enum TransientMessageType
    {
        None,
        Progress,
        Info,
        Error
    }
}
