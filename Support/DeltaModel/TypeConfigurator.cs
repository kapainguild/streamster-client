using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Clutch.DeltaModel
{
    public class TypeConfigurator
    {
        public TypeConfiguration Configuration { get; set; } = new TypeConfiguration();

        public Dictionary<string, TypeMemberConfigurator> Members { get; } = new Dictionary<string, TypeMemberConfigurator>();
    }

    public class TypeConfigurator<T> : TypeConfigurator
    {
        public TypeMemberConfigurator<S> Property<S>(Expression<Func<T, S>> getter)
        {
            return GetOrCreateMember<S>(GetMemberFromExpression(getter), typeof(S));
        }

        private string GetMemberFromExpression<S>(Expression<Func<T, S>> getter)
        {
            var memberExpression = (MemberExpression)getter.Body;
            return memberExpression.Member.Name;
        }

        private TypeMemberConfigurator<S> GetOrCreateMember<S>(string name, Type propertyType)
        {
            if (!Members.TryGetValue(name, out TypeMemberConfigurator member))
            {
                member = new TypeMemberConfigurator<S>(propertyType);
                Members[name] = member;
            }
            return (TypeMemberConfigurator<S>)member;
        }

        public void HasLocal(Func<T, DeltaModelManager, IDeltaServiceProvider, object> creator)
        {
            Configuration.LocalCreator = (o, m) => creator((T)o, m, m.DeltaServiceProvider);
        }
    }
}
