using DynamicStreamer.Contexts;
using DynamicStreamer.DirectXHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicStreamer.Extensions
{
    public class HardwareEncoderCheck
    {
        HardwareEncoderCheckResult _result = null;

        public void Start()
        {
            ThreadPool.QueueUserWorkItem(s => Test());
        }

        public HardwareEncoderCheckResult GetResult()
        {
            lock (this)
            {
                while (_result == null)
                    Monitor.Wait(this);

                return _result;
            }
        }

        private void Test()
        {
            var adapters = DirectXContextFactory.GetAdapters();
            bool qsv = Test<EncoderContext>("h264_qsv", $"preset{Core.Eq}medium{Core.Sep}bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main");
            bool qsv_nv12 = false;
            if (qsv)
            {
                var qsvAdapter = adapters.FirstOrDefault(s => s.Vendor == AdapterVendor.Intel);
                if (qsvAdapter != null)
                {
                    qsv_nv12 = TestQsvNv12(qsvAdapter);
                }
            }
            bool nv = Test<EncoderContext>("h264_nvenc", $"bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}rc{Core.Eq}cbr_ld_hq{Core.Sep}zerolatency{Core.Eq}1{Core.Sep}preset{Core.Eq}ll");
            bool amd = Test<EncoderContext>("h264_amf", $"usage{Core.Eq}webcam{Core.Sep}bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}rc{Core.Eq}cbr{Core.Sep}quality{Core.Eq}balanced");

            string report = string.Join(", ", new[]{ qsv ? "QSV" : null, qsv_nv12 ? "QSV_NV12" : null, nv ? "NVidia" : null, amd ? "AMD" : null}.Where(s => s != null));
            Core.LogInfo("Encoder hardware check: '" + (string.IsNullOrEmpty(report) ? "None" : report) + "'");

            lock (this)
            {
                _result = new HardwareEncoderCheckResult(qsv, qsv_nv12, nv, amd);
                Monitor.PulseAll(this);
            }
        }

        private bool TestQsvNv12(AdapterInfo qsvAdapter)
        {
            try
            {
                var options = new VideoRenderOptions(VideoRenderType.HardwareSpecific, qsvAdapter.Name, IntPtr.Zero, false, 0);
                var dx = DirectXContextFactory.CreateDevice(options);
                using var dxContext = new DirectXContext(dx.Item1, options, dx.Item2, dx.Item3, null);

                if (!dxContext.Nv12Supported)
                {
                    Core.LogInfo($"Device {qsvAdapter.Name} does not have n12 format support");
                }

                if (Test<EncoderContextQsvDx>("", "", EncoderContextQsvDx.TypeName, dxContext))
                {
                    Core.LogInfo($"Device {qsvAdapter.Name} successfully tested for nv12 support");
                    return true;
                }
            }
            catch (Exception e)
            {
                Core.LogError(e, $"Device {qsvAdapter.Name} failed in test for nv12 support");
            }
            return false;
        }

        private bool Test<T>(string enc, string options, string type = null, DirectXContext dx = null) where T: IEncoderContext, new()
        {
            var ctx = new T();
            int res = ctx.Open(new EncoderSetup
            {
                Type = type,
                Name = enc,
                Options = options,
                EncoderBitrate = new EncoderBitrate { bit_rate = 4000, max_rate = 4000, buffer_size = 5000},
                DirectXContext = dx,
                EncoderSpec = new EncoderSpec
                {
                    width = 1280,
                    height = 720,
                    Quality = VideoEncoderQuality.Balanced,
                    time_base = new AVRational { num = 1, den = 30 },
                    sample_aspect_ratio = new AVRational { num = 0, den = 1 }
                }
            });

            ctx.Dispose();

            return res >= 0;
        }

        
    }

    public record HardwareEncoderCheckResult(bool Qsv, bool Qsv_nv12, bool Nv, bool Amd);
}
