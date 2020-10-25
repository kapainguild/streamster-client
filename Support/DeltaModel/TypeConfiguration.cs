using System;
using System.Collections.Generic;
using System.Text;

namespace Clutch.DeltaModel
{
    public class TypeConfiguration
    {
        public Type Type { get; set; }

        public bool IsExternalType { get; set; }

        public TypeConfiguration ValueType { get; set; }

        public TypeConfiguration KeyType { get; set; }

        public Dictionary<string, TypeMemberConfiguration> Members { get; } = new Dictionary<string, TypeMemberConfiguration>();

        public Func<IEntityHandler, string, object> Creator { get; set; }

        public Func<object, DeltaModelManager, object> LocalCreator { get; set; }

        public object DefaultValue { get; set; }
    }
}
