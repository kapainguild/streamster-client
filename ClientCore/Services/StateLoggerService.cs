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

            var rec = settings.IsRecordingRequested ? ":Rec" : "";


            string streamToCloud = _coreData.Settings.StreamingToCloudStarted ? $"|Bit{kpis.CloudOut.Bitrate}" : null;
            string kpi = null;
            if (kpis != null)
            { 
                kpi = $"P{kpis.Cpu.Load}";
                if (kpis.Encoder?.Data != null)
                    kpi += $":EQ{kpis.Encoder.Data.Q}:EOFps{kpis.Encoder.Data?.O}:FI{kpis.Encoder.Data.F}";
                kpi += streamToCloud;
            }

            var channels = string.Join("|", root.Channels.Values.Where(s => s.IsOn).Select(s => $"Ch:{s.TargetId}:{(int)s.State}:{s.Bitrate}"));

            Log.Information($"State. {settings.Resolution.Height}:{settings.Fps}:{settings.Bitrate}:{settings.EncoderType}:{settings.EncoderQuality}{rec}|{kpi}|{channels}");
        }
    }
}
