using System;
using System.Runtime.InteropServices;

namespace Streamster.ClientApp.Win.Services
{
    static class SleepHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);


        public static void PreventSleepMode(bool bEnable)
        {
            EXECUTION_STATE state = EXECUTION_STATE.ES_CONTINUOUS;
            if (bEnable)
                state |= EXECUTION_STATE.ES_DISPLAY_REQUIRED;

            SetThreadExecutionState(state);
        }
    }

    [Flags]
    public enum EXECUTION_STATE : uint
    {
        ES_AWAYMODE_REQUIRED = 0x00000040,
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001
    }
}
