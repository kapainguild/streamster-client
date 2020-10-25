using Serilog;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Services
{
    public class StateLoggerService
    {
        private readonly CoreData _coreData;

        public StateLoggerService(CoreData coreData)
        {
            _coreData = coreData;
        }

        public void Start()
        {
            TaskHelper.RunUnawaited(() => LoggerRoutine(), "LoggerRoutine");
        }

        private async Task LoggerRoutine()
        {
            while(true)
            {
                try
                {
                    DumpState();
                }
                catch (Exception e)
                {
                    Log.Error(e, "DumpStateFailed");
                }

                await Task.Delay(10000);
            }
        }

        private void DumpState()
        {
            var root = _coreData.Root;
            var kpis = _coreData.ThisDevice.KPIs;
            var settings = root.Settings;

            string video = null;
            if (root.Settings.SelectedVideo != null && root.VideoInputs.TryGetValue(root.Settings.SelectedVideo, out var vi))
                video = GetVideoName(vi);

            string audio = null;
            if (root.Settings.SelectedAudio != null && root.AudioInputs.TryGetValue(root.Settings.SelectedAudio, out var ai))
                audio = ai.Name.Substring(0, Math.Min(12, ai.Name.Length));

            var rec = settings.IsRecordingRequested ? ":Rec" : "";

            string kpi = null;
            if (kpis != null)
                kpi = $"P{kpis.Cpu.Load}:Q{kpis.Encoder.QueueSize}:IFps{kpis.Encoder.InputFps}:TFps{kpis.Encoder.InputTargetFps}:IE{kpis.Encoder.InputErrors}:OBit{kpis.CloudOut.Bitrate}:OE{kpis.CloudOut.Errors}";

            var channels = string.Join("|", root.Channels.Values.Where(s => s.IsOn).Select(s => $"Ch:{s.TargetId}:{(int)s.State}:{s.Bitrate}"));

            Log.Information($"State. {video}:{settings.Resolution.Height}:{settings.Fps}:{audio}:{settings.Bitrate}:{settings.EncoderType}:{settings.EncoderQuality}{rec}|{kpi}|{channels}");
        }

        private string GetVideoName(IVideoInput vi)
        {
            string res = vi.Name.Substring(0, Math.Min(12, vi.Name.Length));
            if (vi.Filters != null)
            {
                if (vi.Filters.FlipH)
                    res += ",flip";
                if (vi.Filters.Items != null)
                    res += string.Join(",", vi.Filters.Items.Select(s => $"{s.Name}={s.Value}"));
            }
            return res;
        }
    }
}
