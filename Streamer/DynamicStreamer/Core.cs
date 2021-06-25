using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicStreamer
{

    public class Core
    {
        public static string Eq = "^";
        public static string Sep = "`";

        public static int PIX_FMT_INTERNAL_DIRECTX = -1;

        public const string DllName = "DynamicStreamerCore.dll";

        private static LogCallbackFunction s_onLogCallbackFunction;
        private static Action<LogType, string, string, Exception> s_onLog;
        private static long s_performanceFrequency;



        [DllImport(Core.DllName)]
        private static extern void Core_Init(IntPtr logCallbackFunction, ref StreamerConstants streamerConstants);

        [DllImport(Core.DllName)]
        private static extern int Core_GetErrorMessage(int error, int bufferLength, ref byte buffer);

        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern bool QueryPerformanceFrequency(out long lpPerformanceFreq);

        delegate void LogCallbackFunction(int severity, [MarshalAs(UnmanagedType.LPStr)]string pattern, [MarshalAs(UnmanagedType.LPStr)] string msg);

        public static StreamerConstants Const { get; private set; }
        public static StreamerConstants2 Const2 { get; private set; }

        public static void Init(Action<LogType, string, string, Exception> onLog)
        {
            QueryPerformanceFrequency(out s_performanceFrequency);

            s_onLog = onLog;
            s_onLogCallbackFunction = new LogCallbackFunction((s, p, m) => onLog((LogType)s, p, "  -" + m.TrimEnd('\n'), null));
            StreamerConstants c = new StreamerConstants();
            Core_Init(Marshal.GetFunctionPointerForDelegate(s_onLogCallbackFunction), ref c);
            Const = c;
            Const2 = new StreamerConstants2(c);

            ExtensionsManager.Init();
        }

        public static void InitOnMain()
        {
            ExtensionsManager.InitOnMain();
        }

        public static long GetCurrentTime()
        {
            long cur;
            QueryPerformanceCounter(out cur);
            return cur * 10_000 / (s_performanceFrequency / 1000);
        }

        public static void Shutdown()
        {
            ExtensionsManager.Shutdown();
        }

        public static byte[] StringToBytes(string input)
        {
            if (input == null)
                return null;
            return Encoding.UTF8.GetBytes(input).Concat(new byte[] { 0 }).ToArray();
        }

        public static string BytesToString(byte[] input)
        {
            if (input == null)
                return null;

            int q = 0;
            for (; q < input.Length; q++)
                if (input[q] == 0)
                    break;
            return Encoding.UTF8.GetString(input, 0, q);
        }

        public static string FormatTicks(long ticks)
        {
            var r = new TimeSpan(ticks);
            return $"{(int)r.TotalMinutes} {r.Seconds:00}.{r.Milliseconds:000}";
        }

        public static string FormatDouble(double d, int fractions)
        {
            var format = $"F{fractions}";
            return d.ToString(format, CultureInfo.InvariantCulture);
        }

        public static bool IsOk(ErrorCodes er) => ((int)er) >= 0;
        
        public static bool IsOk(int er) => er >= 0;

        public static bool IsFailed(ErrorCodes er) => ((int)er) < 0;
        
        public static bool IsFailed(int er) => er < 0;
        

        public static string GetErrorMessage(int error)
        {
            if (error >= 0)
                return $"Success({error})";
            else if (error == (int)ErrorCodes.ContextIsNotOpened)
                return "Failed - Context is not opened";
            else
            {
                var buffer = new byte[1024];
                if (Core_GetErrorMessage(error, buffer.Length, ref buffer[0]) >= 0)
                    return $"Failed({BytesToString(buffer)})";
                else
                    return $"Failed({error})";
            }
        }

        private static void LogDotNet(LogType type, string message, string template = null, Exception e = null)
        {
            s_onLog(type, template, message, e);
        }

        public static void LogInfo(string message, string template = null) => LogDotNet(LogType.Info, message, template);

        public static void LogWarning(string message, string template = null) => LogDotNet(LogType.Warning, message, template);

        public static void LogError(string message, string template = null) => LogDotNet(LogType.Error, message, template);

        public static void LogError(Exception e, string message) => LogDotNet(LogType.Error, $"{message}: {e.Message}", null, e);

        public static void Checked(int value, string error)
        {
            if (value < 0)
            {
                if (value == (int)ErrorCodes.TimeoutOrInterrupted)
                    throw new OperationCanceledException();

                string msg = $"{error}: {GetErrorMessage(value)}";
                throw new DynamicStreamerException(msg, value);
            }
        }
    }

    public enum LogType
    {
        Info = 1,
        Warning,
        Error
    }
}
