using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientData.Model
{
    public class DeviceName
    {
        public string DeviceId { get; set; }

        public string Name { get; set; }

        public override bool Equals(object obj)
        {
            return obj is DeviceName name &&
                   DeviceId == name.DeviceId &&
                   Name == name.Name;
        }

        public override int GetHashCode()
        {
            int hashCode = -1919740922;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(DeviceId);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            return hashCode;
        }

        public override string ToString() => $"{Name}";
    }

    public interface IInputDevice
    {
        string Name { get; set; }

        InputDeviceType Type { get; set; }

        InputDeviceState State { get; set; }
    }

    public enum InputDeviceType
    {
        USB = 0,
        Virtual = 1,
        Remote = 2
    }

    public enum InputDeviceState
    {
        Unknown = 0,

        Ready = 1,
        Removed = 3,
        Failed = 4,
        NotStarted = 5,
        Locked = 6,
    }
}
