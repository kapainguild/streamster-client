
namespace Streamster.ClientData.Model
{
    public interface IDeviceSettings
    {
        IntRect NormalWnd { get; set; }

        IntRect CompactWnd { get; set; }

        AppWindowState AppWindowState { get; set; }

        bool DisableTopMost { get; set; }

        bool TopMostExtendedMode { get; set; }

        string RecordingsPath { get; set; }
    }

    public enum AppWindowState
    {
        Normal,
        Compact,
        FullScreen,
        Maximized,
        Minimized
    }

    public class IntRect
    {
        public int L { get; set; }
        public int T { get; set; }
        public int W { get; set; }
        public int H { get; set; }
    }


    public enum TopMostMode
    {
        Always,
        WhenCompact,
        Never,
        Manual
    }

    public static class TopMostModeConverter
    {
        public static TopMostMode ToMode(IDeviceSettings d)
        {
            if (d.TopMostExtendedMode)
            {
                if (d.DisableTopMost)
                    return TopMostMode.Manual;
                else
                    return TopMostMode.Always;
            }
            else
            {
                if (d.DisableTopMost)
                    return TopMostMode.Never;
                else
                    return TopMostMode.WhenCompact;
            }
        }

        public static bool GetDisableTopMost(TopMostMode m) => m == TopMostMode.Manual || m == TopMostMode.Never;

        public static bool GetTopMostExtendedMode(TopMostMode m) => m == TopMostMode.Manual || m == TopMostMode.Always;
    }
}
