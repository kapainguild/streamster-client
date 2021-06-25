using SharpDX;
using System.IO;
using System.Reflection;

namespace DynamicStreamer.DirectXHelpers
{
    public static class DirectXHelper
    {

        public static string ReadResource(string name, string folder = "Shaders", string defaultNamespace = "DynamicStreamer", Assembly asm = null)
        {
            var assembly = asm ?? Assembly.GetExecutingAssembly();
            var resourceName = $"{defaultNamespace}.{folder}.{name}";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        public static byte[] ReadResourceAsBytes(string name, string folder = "LUTs", string defaultNamespace = "DynamicStreamer", Assembly asm = null)
        {
            var assembly = asm ?? Assembly.GetExecutingAssembly();
            var resourceName = $"{defaultNamespace}.{folder}.{name}";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                var buf = new byte[stream.Length];
                stream.Read(buf);
                return buf;
            }
        }
    }
}
