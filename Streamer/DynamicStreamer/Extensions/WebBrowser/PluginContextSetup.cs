using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicStreamer.Extensions.WebBrowser
{
    public static class PluginContextSetup
    {
        public const string SubPath = "Streamster.Lovense\\Plugins";
        public const string DllName = "webbasedplugin.dll";

        private static bool s_justInstalled = false;
        private static IntPtr s_handle = IntPtr.Zero;


        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int AddDllDirectory(string lpPathName);

        const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
        private static bool _displayed;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDefaultDllDirectories(uint DirectoryFlags);


        [DllImport(DllName)]
        private static extern void PluginWillAddToScene();
        [DllImport(DllName)]
        private static extern bool PluginShouldAddToSceneWhenDllLoadSuccess();
        [DllImport(DllName)]
        private static extern IntPtr Plugin_Init();
        [DllImport(DllName)]
        private static extern void Plugin_Destruct(IntPtr handle);


        public static void DisplayMessage()
        {
            if (!_displayed)
            {
                _displayed = true;
                PluginWillAddToScene();
            }
        }

        public static bool AddPlugin()
        {
            return PluginShouldAddToSceneWhenDllLoadSuccess();
        }

        public static bool PluginDllExists()
        {
            try
            {
                return File.Exists(Path.Combine(GetPath(), DllName));
            }
            catch (Exception e)
            {
                Core.LogError(e, "Lovense PluginDllExists failed");
            }
            return false;
        }

        public static bool IsJustInstalled() => s_justInstalled;

        public static IntPtr GetHandle() => s_handle;

        public static bool IsLoaded() => s_handle != IntPtr.Zero;

        public static bool TryToLoad()
        {
            if (s_handle != IntPtr.Zero)
                return true;

            if (PluginDllExists())
            {
                try
                {
                    PrepareLoad();

                    s_justInstalled = PluginShouldAddToSceneWhenDllLoadSuccess();
                    s_handle = Plugin_Init();
                    Core.LogInfo("Lovense loaded");
                    return true;
                }
                catch (Exception e)
                {
                    Core.LogError(e, "Lovense TryToLoad failed");
                }
            }
            return false;
        }

        public static void Unload()
        {
            if (s_handle != IntPtr.Zero)
            {
                Plugin_Destruct(s_handle);
                s_handle = IntPtr.Zero;
            }
        }

        public static bool IsInstalledForOthers()
        {
            try
            {
                return Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lovense")) ||
                    File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"obs-studio\obs-plugins\64bit\Lovense_Update.dll"));
            }
            catch (Exception e)
            {
                Core.LogError(e, "Lovense IsInstalledForOthers failed");
            }
            return false;
        }

        public static string GetPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), SubPath);

        public static void PrepareLoad()
        {
            var res = SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
            Core.LogInfo($"SetDefaultDllDirectories returned {res}");

            var path = GetPath();
            if (Directory.Exists(path))
            {
                var rr = AddDllDirectory(path);
                Core.LogInfo($"Path {path} added {rr}");

                if (!File.Exists(Path.Combine(path, "webbasedplugin.dll")))
                   Core.LogWarning($"File webbasedplugin.dll not found");
            }
            else Core.LogWarning($"Path {path} not found");
        }

    }
}
