using System;

namespace Clutch.DeltaModel
{
    public class TypeMemberConfigurator
    {
        public TypeMemberConfiguration Configuration { get; }

        public TypeMemberConfigurator(Type type)
        {
            Configuration = new TypeMemberConfiguration(type);
        }

        public void DontCompareBeforeSet()
        {
            Configuration.DontCompareBeforeSet = true;
        }
    }

    public class TypeMemberConfigurator<T> : TypeMemberConfigurator
    {
        public TypeMemberConfigurator(Type type) : base(type)
        {
        }

        public TypeMemberConfigurator<T> HasDefault(T defVal) // VERY dangerous
        {
            Configuration.DefaultValue = defVal;
            return this;
        }
    }
}
