using Clutch.DeltaModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeltaModel
{
    public class DeltaModelBuilder
    {
        private Dictionary<Type, TypeConfigurator> _configurators = new Dictionary<Type, TypeConfigurator>();

        private Dictionary<Type, TypeConfiguration> _configurations = new Dictionary<Type, TypeConfiguration>();

        public TypeConfigurator<T> Config<T>(Action<TypeConfigurator<T>> configuration)
        {
            var type = typeof(T);
            if (!_configurators.TryGetValue(type, out TypeConfigurator result))
            {
                result = new TypeConfigurator<T>();
                _configurators[type] = result;
            }
            var typed = (TypeConfigurator<T>)result;
            configuration(typed);
            return typed;
        }

        public DeltaModelManager<T> Build<T>(IDeltaServiceProvider deltaServiceProvider = null, ILockerProvider locker = null)
        {

            BuildModel(typeof(T));

            if (locker == null)
                locker = new MultiThreadLockerProvider();
            var manager = new DeltaModelManager<T>(_configurations, deltaServiceProvider, locker);
            locker.SetManager(manager);
            return manager;
        }

        private TypeConfiguration BuildModel(Type type)
        {
            if (_configurations.TryGetValue(type, out var typeConfig))
                return typeConfig;

            if (!_configurators.TryGetValue(type, out var configurator))
                configurator = new TypeConfigurator();

            typeConfig = configurator.Configuration;
            typeConfig.Type = type;
            _configurations[type] = typeConfig;

            if (type.IsValueType)
            {
                typeConfig.DefaultValue = Activator.CreateInstance(type);
            }
            else
            {
                typeConfig.DefaultValue = null;
            }

            if (type.IsGenericType)
            {
                var generic = type.GetGenericTypeDefinition();
                var args = type.GetGenericArguments();
                if (generic == typeof(IDictionary<,>))
                {
                    typeConfig.KeyType = BuildModel(args[0]);
                    typeConfig.ValueType = BuildModel(args[1]);
                }
                else
                    typeConfig.IsExternalType = true;
            }
            else if (type.IsInterface)
            {
                var props = new[] { type }.Concat(type.GetInterfaces()).SelectMany(s => s.GetProperties()).ToArray();
                foreach (var prop in props)
                {
                    if (!typeConfig.Members.TryGetValue(prop.Name, out var propConfig))
                    {
                        if (configurator.Members.TryGetValue(prop.Name, out var propConfigurator))
                            propConfig = propConfigurator.Configuration;
                        else
                            propConfig = new TypeMemberConfiguration(prop.PropertyType);
                        typeConfig.Members[prop.Name] = propConfig;
                    }
                    propConfig.TypeConfiguration = BuildModel(prop.PropertyType);
                }
            }
            else 
                typeConfig.IsExternalType = true;

            return typeConfig;
        }
    }
}
