using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace DynamicStreamer
{
    public class FilterContextFFMpeg : IFilterContext
    {
        public static string Type = nameof(FilterContextFFMpeg);

        private IntPtr _handle;
        private bool _opened;
        private int _inputs = 0;

        [DllImport(Core.DllName)] private static extern IntPtr FilterContext_Create();
        [DllImport(Core.DllName)] private static extern void FilterContext_Delete(IntPtr handle);
        [DllImport(Core.DllName)] private static extern int FilterContext_Open(IntPtr handle, ref FilterInputSpec inputSpec, int inputs, ref FilterOutputSpec outputSpec, byte[] filterSpec);
        [DllImport(Core.DllName)] private static extern int FilterContext_Write(IntPtr handle, IntPtr packet, int inputNo);
        [DllImport(Core.DllName)] private static extern ErrorCodes FilterContext_Read(IntPtr handle, IntPtr frame, ref FrameProperties frameProperties);

        public FilterContextFFMpeg()
        {
            _handle = FilterContext_Create();
        }

        public int Open(FilterSetup filterSetup)
        {
            var inputSpecs = filterSetup.InputSetups.Select(s => s.FilterSpec).ToArray();
            _inputs = inputSpecs.Length;
            int res = FilterContext_Open(_handle, ref inputSpecs[0], inputSpecs.Length, ref filterSetup.OutputSpec, Core.StringToBytes(filterSetup.FilterSpec));
            _opened = res >= 0;
            return res;
        }

        public int Write(Frame frame, int inputNo)
        {
            if (_opened)
            {
                if (inputNo < _inputs)
                    return FilterContext_Write(_handle, frame?.Handle ?? IntPtr.Zero, inputNo);
                else
                {
                    Core.LogWarning($"Providing wrong input {inputNo} < {_inputs} to filter");
                    return 0;
                }
            }
            else
                return (int)ErrorCodes.ContextIsNotOpened;
        }

        public ErrorCodes Read(Frame frame)
        {
            if (_opened)
            {
                var res = FilterContext_Read(_handle, frame.Handle, ref frame.Properties);
                return res;
            }
            else
                return ErrorCodes.ContextIsNotOpened;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                FilterContext_Delete(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
