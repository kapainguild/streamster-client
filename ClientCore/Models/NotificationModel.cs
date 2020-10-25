using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public class NotificationModel
    {
        public Property<NotificationType> Type { get; } = new Property<NotificationType>(NotificationType.None);

        public Property<string> Message { get; } = new Property<string>();
    }

    public enum NotificationType
    {
        None,
        Progress,
        Info,
        Error
    }
}
