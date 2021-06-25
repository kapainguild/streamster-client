using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientData.Model
{
    public enum ResourceType { LutPng, LutCube, ImagePng, ImageJpeg }

    public interface IResource
    {
        ResourceInfo Info { get; set;}

        DateTime LastUse { get; set; }

        byte[] Data { get; set; }
    }

    public class ResourceInfo
    {
        public string Name { get; set; }

        public ResourceType Type { get; set; }

        public string DataHash { get; set; }

        public DateTime Added { get; set; }
    }
}
