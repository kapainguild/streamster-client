using Serilog;
using Streamster.ClientCore.Models;
using System;

namespace Streamster.ClientCore.Services
{
    public class NotificationService
    {
        public NotificationModel Model { get; } = new NotificationModel();

        internal void SetError(object source, string message, Exception e = null)
        {
            Model.Type.Value = NotificationType.Error;
            if (message != Model.Message.Value)
            {
                Log.Warning(e, message);
                Model.Message.Value = message;
            }
        }

        public void SetProgress(string message)
        {
            Model.Type.Value = NotificationType.Progress;
            Model.Message.Value = message;
            Log.Information(message);
        }

        internal void Clear(object source)
        {
            Model.Message.Value = null;
            Model.Type.Value = NotificationType.None;
        }
    }
}
