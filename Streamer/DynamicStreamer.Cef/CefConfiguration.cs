using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamerCef
{
    public class CefConfiguration
    {
        public string CachePathRequest { get; set; }
        public string CachePathRoot { get; set; }
        public string CachePathGlobal { get; set; }

        public string LogFile { get; set; }
        public bool LogVerbose { get; set; }
    }
}
