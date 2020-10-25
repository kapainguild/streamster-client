using System;
using System.Runtime.InteropServices;

namespace Streamster.ClientApp.Win.Services
{
    class InputSender
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int sizeOfInputStructure);

        public static void Send(CaptionButtonType dock)
        {
            ushort keyCode = dock == CaptionButtonType.DockLeft ? (ushort)0x25 : (ushort)0x27; // left: right
            var inputs = new[]
            {
                new INPUT
                {
                    Type = 1,
                    Data = new MOUSEKEYBDHARDWAREINPUT
                    {
                        Keyboard = new KEYBDINPUT
                        {
                             Vk = (ushort)0x5b, //LWIN
                             Flags = 0
                        }
                    }
                },
                new INPUT
                {
                    Type = 1,
                    Data = new MOUSEKEYBDHARDWAREINPUT
                    {
                        Keyboard = new KEYBDINPUT
                        {
                             Vk = keyCode,
                             Flags = 1
                        }
                    }
                },
                new INPUT
                {
                    Type = 1,
                    Data = new MOUSEKEYBDHARDWAREINPUT
                    {
                        Keyboard = new KEYBDINPUT
                        {
                             Vk = keyCode,
                             Flags = 3
                        }
                    }
                },
                new INPUT
                {
                    Type = 1,
                    Data = new MOUSEKEYBDHARDWAREINPUT
                    {
                        Keyboard = new KEYBDINPUT
                        {
                             Vk = (ushort)0x5b,//LWIN
                             Flags = 2
                        }
                    }
                },
            };

            var res = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            public uint Type;
            public MOUSEKEYBDHARDWAREINPUT Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct MOUSEKEYBDHARDWAREINPUT
        {
            [FieldOffset(0)]
            public HARDWAREINPUT Hardware;
            [FieldOffset(0)]
            public KEYBDINPUT Keyboard;
            [FieldOffset(0)]
            public MOUSEINPUT Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HARDWAREINPUT
        {
            public uint Msg;
            public ushort ParamL;
            public ushort ParamH;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            public ushort Vk;
            public ushort Scan;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }
    }
}
