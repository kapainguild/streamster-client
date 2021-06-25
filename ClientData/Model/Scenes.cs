using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Streamster.ClientData.Model
{
    public interface IScene
    {
        IDictionary<string, ISceneItem> Items { get; set; }

        IDictionary<string, ISceneAudio> Audios { get; set; }

        InputIssue[] VideoIssues { get; set; }

        InputIssue[] AudioIssues { get; set; }

        string Owner { get; set; }
    }

    public interface ISceneAudio
    {
        bool Muted { get; set; }

        double Volume { get; set; }

        SceneAudioSource Source { get; set;}
    }

    public static class SceneAudioConsts
    {
        public static string MicrophoneId = "Mic";
        public static string DesktopAudioId = "DesktopAudio";
    }

    public class SceneAudioSource
    {
        public bool DesktopAudio { get; set; }

        public DeviceName DeviceName { get; set; }
    }

    public class SceneRect
    {
        public SceneRect() {}
        public SceneRect(double l, double t, double width, double height) { L = l; T = t; W = width; H = height;}

        public double Bottom() => T + H;
        public double Right() => L + W;
        public bool Contains(double x, double y) => x >= L && x - W <= L && y >= T && y - H <= T;
        public static SceneRect Full() => new SceneRect(0, 0, 1, 1);

        public override bool Equals(object obj) => obj is SceneRect rect && T == rect.T && L == rect.L && W == rect.W && H == rect.H;

        public override int GetHashCode() => T.GetHashCode();

        public double T { get; set; }
        public double L { get; set; }
        public double W { get; set; }
        public double H { get; set; }
    }


    public interface ISceneItem
    {
        SceneRect Rect { get; set; }

        SceneRect Ptz { get; set; }

        ZoomResolutionBehavior ZoomBehavior { get; set;}

        bool Visible { get; set; }

        int ZOrder { get; set; }

        SceneItemSource Source { get; set; }

        SceneItemFilters Filters { get; set; }
    }

    public enum ZoomResolutionBehavior { Never, DependingOnZoom, Always }

    public class SceneItemSource
    {
        public SceneItemSourceDevice Device { get; set; }

        public SceneItemSourceWeb Web { get; set; }

        public SceneItemSourceLovense Lovense { get; set; }

        public SceneItemSourceImage Image { get; set; }

        public SceneItemSourceCapture CaptureDisplay { get; set; }

        public SceneItemSourceCapture CaptureWindow { get; set; }

        public override bool Equals(object obj)
        {
            return obj is SceneItemSource source &&
                   EqualityComparer<SceneItemSourceDevice>.Default.Equals(Device, source.Device) &&
                   EqualityComparer<SceneItemSourceWeb>.Default.Equals(Web, source.Web) &&
                   EqualityComparer<SceneItemSourceLovense>.Default.Equals(Lovense, source.Lovense) &&
                   EqualityComparer<SceneItemSourceImage>.Default.Equals(Image, source.Image) &&
                   EqualityComparer<SceneItemSourceCapture>.Default.Equals(CaptureDisplay, source.CaptureDisplay) &&
                   EqualityComparer<SceneItemSourceCapture>.Default.Equals(CaptureWindow, source.CaptureWindow);
        }

        public override int GetHashCode()
        {
            int hashCode = -607031459;
            hashCode = hashCode * -1521134295 + EqualityComparer<SceneItemSourceDevice>.Default.GetHashCode(Device);
            hashCode = hashCode * -1521134295 + EqualityComparer<SceneItemSourceWeb>.Default.GetHashCode(Web);
            hashCode = hashCode * -1521134295 + EqualityComparer<SceneItemSourceLovense>.Default.GetHashCode(Lovense);
            hashCode = hashCode * -1521134295 + EqualityComparer<SceneItemSourceImage>.Default.GetHashCode(Image);
            hashCode = hashCode * -1521134295 + EqualityComparer<SceneItemSourceCapture>.Default.GetHashCode(CaptureDisplay);
            hashCode = hashCode * -1521134295 + EqualityComparer<SceneItemSourceCapture>.Default.GetHashCode(CaptureWindow);
            return hashCode;
        }

        public override string ToString()
        {
            var items = new object[] { Device, Web, Lovense, Image, CaptureDisplay, CaptureWindow };
            return string.Join("|", items.Where(s => s != null));
        }
    }

    public class SceneItemSourceDevice
    {
        public DeviceName DeviceName { get; set; }

        public override bool Equals(object obj)
        {
            return obj is SceneItemSourceDevice device &&
                   EqualityComparer<DeviceName>.Default.Equals(DeviceName, device.DeviceName);
        }

        public override int GetHashCode()
        {
            return -1881315784 + EqualityComparer<DeviceName>.Default.GetHashCode(DeviceName);
        }

        public override string ToString() => $"Device {DeviceName?.Name}";
    }

    public class SceneItemSourceWeb
    {
        public string Url { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public override bool Equals(object obj)
        {
            return obj is SceneItemSourceWeb web &&
                   Url == web.Url &&
                   Width == web.Width &&
                   Height == web.Height;
        }

        public override int GetHashCode()
        {
            int hashCode = -1388710761;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Url);
            hashCode = hashCode * -1521134295 + Width.GetHashCode();
            hashCode = hashCode * -1521134295 + Height.GetHashCode();
            return hashCode;
        }

        public override string ToString() => $"Web {Url} ({Width}x{Height})";
    }

    public class SceneItemSourceLovense
    {
        public override string ToString() => $"Lovense";
    }

    public class SceneItemSourceImage
    {
       public  string ResourceId { get; set; }

        public override bool Equals(object obj)
        {
            return obj is SceneItemSourceImage image &&
                   ResourceId == image.ResourceId;
        }

        public override int GetHashCode()
        {
            return -419350640 + EqualityComparer<string>.Default.GetHashCode(ResourceId);
        }

        public override string ToString() => $"Image (id:{ResourceId})";
    }

    public class SceneItemSourceCapture
    {
        public CaptureSource Source { get; set; }

        public bool CaptureCursor { get; set; }

        public override bool Equals(object obj)
        {
            return obj is SceneItemSourceCapture capture &&
                   Equals(Source, capture.Source) &&
                   CaptureCursor == capture.CaptureCursor;
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override string ToString() => $"Capture (Source {Source})";
    }

    public class SceneItemFilters
    {
        public SceneItemFilter[] Filters { get; set; }
    }

    public class SceneItemFilter
    {
        public SceneItemFilterType Type { get; set; }

        public bool Enabled { get; set; }

        public double Value { get; set; }

        public string LutResourceId { get; set; }
    }

    public enum SceneItemFilterType
    {
        None,

        HFlip,

        Warm,
        Cold,
        Dark,
        Light,
        Vintage,
        Sepia,
        Grayscale,

        Contrast,
        Brightness,
        Saturation,
        Gamma,

        Hue,
        Opacity,
        Sharpness,

        UserLut,

        Azure,
        B_W,
        Chill,
        Pastel,
        Romantic,
        Sapphire,
        Wine



    }


    public class InputIssue
    {
        public string Id { get; set; }

        public InputIssueDesc Desc { get; set; }

        public override bool Equals(object obj)
        {
            return obj is InputIssue issue &&
                   Id == issue.Id &&
                   Desc == issue.Desc;
        }

        public override int GetHashCode()
        {
            int hashCode = 1737832512;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Id);
            hashCode = hashCode * -1521134295 + Desc.GetHashCode();
            return hashCode;
        }
    }

    public enum InputIssueDesc
    {
        None,

        // init time consts
        NoAudioSelected, //"No microphone selected"
        AudioRemoved,

        UnknownTypOfSource,

        VideoRemoved,
        ImageNotFound,
        ImageUnknownFormat,
        PluginIsNotInstalled, // $"Lovense plugin is not installed or failed to load"
        CaptureNotFound,

        // runtime consts
        Failed,
        NoFrames,
        TooManyFrames,
        InUse,
    }
}
