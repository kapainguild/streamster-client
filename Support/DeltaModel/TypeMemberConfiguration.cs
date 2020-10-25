using System;

namespace Clutch.DeltaModel
{
    public class TypeMemberConfiguration
    {
        public Type PropertyType { get; set; }

        public TypeConfiguration TypeConfiguration { get; set; }

        public bool DontCompareBeforeSet { get; set; }

        public object DefaultValue { get; set; }

        public TypeMemberConfiguration(Type propertyType)
        {
            PropertyType = propertyType;
        }
    }
}
