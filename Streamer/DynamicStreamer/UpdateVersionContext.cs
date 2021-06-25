using System;
using System.Collections.Generic;

namespace DynamicStreamer
{
    public class UpdateVersionContext
    {
        private readonly List<Action> _deploys = new List<Action>();

        public UpdateVersionContext(int versionToRun)
        {
            Version = versionToRun;
            RuntimeConfig = new StreamerRuntimeConfig { Version = versionToRun };
        }

        public int Version { get; set; }

        public StreamerRuntimeConfig RuntimeConfig { get; } 

        public void DeployVersion()
        {
            foreach (var item in _deploys)
            {
                item();
            }
        }

        public void AddDeploy(Action action) => _deploys.Add(action);
    }
}
