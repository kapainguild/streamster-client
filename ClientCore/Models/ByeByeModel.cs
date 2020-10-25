using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public class ByeByeModel
    {
        public ByeByeModel(RootModel root)
        {
            Root = root;
        }

        public RootModel Root { get; }

        public bool Loaded { get; } = true;
    }
}
