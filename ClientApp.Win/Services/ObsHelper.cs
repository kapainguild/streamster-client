using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Streamster.ClientApp.Win.Services
{
    static class ObsHelper
    {
        public static HashSet<string> s_loggedNames = new HashSet<string>();

        private static ObsStream[] Streams =
        {
            new ObsStream { QueueName = "OBSVirtualVideo", DeviceName="obs-camera", Index = 0 },
            new ObsStream { QueueName = "OBSVirtualVideo2", DeviceName="obs-camera2", Index = 1 },
            new ObsStream { QueueName = "OBSVirtualVideo3", DeviceName="obs-camera3", Index = 2 },
            new ObsStream { QueueName = "OBSVirtualVideo4", DeviceName="obs-camera4", Index = 3 },
            new ObsStream { QueueName = "OBSVirtualCamVideo", DeviceName="obs virtual camera", Index = 0 },
            
            new ObsStream { QueueName = "OBSVirtualAudio", DeviceName="obs-audio", Index = -1 },
        };

        public static bool IsObsAudioVideoAndItIsOff(string name)
        {
            var obsStream = ObsHelper.GetObsStream(name);
            if (obsStream != null)
            {
                // obs camera;
                if (ObsHelper.TryGetStreamInfo(obsStream.QueueName, out var header))
                {
                    lock (s_loggedNames)
                    {
                        if (!s_loggedNames.Contains(name))
                        {
                            s_loggedNames.Add(name);
                            if (obsStream.Index < 0)
                            {
                                Log.Information($"OBS check {name}: {header.recommended_width}");
                            }
                            else
                            {
                                Log.Information($"OBS check {name}: {header.recommended_width}x{header.recommended_height}x{header.delay_frame}x{header.frame_time}x{header.aspect_ratio_type}");
                            }
                        }
                    }
                }
                else
                    return true;
            }
            return false;
        }

        public static ObsStream GetObsStream(string name)
        {
            name = name?.ToLower();
            return Streams.FirstOrDefault(s => s.DeviceName == name);
        }

        public static void GetObsVersion(out string obs, out string obsCam)
        {
            obs = null;
            obsCam = null;
            try
            {
                var baseRegistry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

                var keyObs = baseRegistry.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\OBS Studio", false);
                if (keyObs != null)
                {
                    if (keyObs.GetValue("UninstallString") is string path)
                    {
                        var basePath = Path.GetDirectoryName(path.Trim('\"'));
                        string programPath = Path.Combine(basePath, "bin\\64bit\\obs64.exe");
                        var version = FileVersionInfo.GetVersionInfo(programPath);
                        obs = version.ProductVersion;

                        string camPath = Path.Combine(basePath, "obs-plugins\\64bit\\obs-virtualoutput.dll");
                        if (File.Exists(camPath))
                        {
                            obsCam = FileVersionInfo.GetVersionInfo(camPath).ProductVersion;
                        }
                    }
                    else
                        throw new InvalidOperationException("UninstallString not found");
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to get obs version");
            }
        }

        private static int GetFps(ulong headerFrameTime)
        {
            return (int)(1000000000ul / headerFrameTime);
        }

        private static string GetShortName(int streamIndex)
        {
            if (streamIndex < 0)
                return "A";
            return $"V{streamIndex}";
        }

        public static bool TryGetStreamInfo(string name, out QueueHeader header)
        {
            bool result = false;
            header = default(QueueHeader);
            var handle = OpenFileMapping(4, false, name);

            if (handle != IntPtr.Zero)
            {
                var view = MapViewOfFileEx(handle, 4, 0, 0, UIntPtr.Zero, IntPtr.Zero);

                if (view != IntPtr.Zero)
                {
                    header = Marshal.PtrToStructure<QueueHeader>(view);
                    result = true;
                    UnmapViewOfFile(view);
                }

                CloseHandle(handle);
            }
            return result;
        }


        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll")]
        static extern IntPtr MapViewOfFileEx(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap, IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hHandle);

    }

    public struct QueueHeader
    {
        public int state;
        public int format;
        public int queue_length;
        public int write_index;
        public int header_size;
        public int element_size;
        public int element_header_size;
        public int delay_frame;
        public int recommended_width;
        public int recommended_height;
        public int aspect_ratio_type;
        public ulong last_ts;
        public ulong frame_time;
    };

    class ObsStream
    {
        public string QueueName { get; set; }

        public string DeviceName { get; set; }

        public int Index { get; set; }

    }

}
