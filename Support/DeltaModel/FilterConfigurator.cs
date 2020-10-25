using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Clutch.DeltaModel
{
    public class FilterConfigurator
    {
        private Dictionary<Type, FilterTypeConfigurator> _types = new Dictionary<Type, FilterTypeConfigurator>();
        private readonly bool _defaultValue;

        public FilterConfigurator(bool defaultValue)
        {
            _defaultValue = defaultValue;
        }

        public void Deny<T>()
        {
            GetOrCreate<T>().IsDeny = true;
        }

        public FilterConfigurator Conditional<T>(Func<T, bool> isAllowed, Action<FilterTypeConfigurator<T>> configure)
        {
            var configurator = GetOrCreate<T>();
            configurator.IsAllowed = (o) => isAllowed((T)o);
            configure(configurator);
            return this;
        }

        public FilterConfigurator Allow<T>(Action<FilterTypeConfigurator<T>> configure)
        {
            configure(GetOrCreate<T>());
            return this;
        }

        private FilterTypeConfigurator<T> GetOrCreate<T>()
        {
            if (_types.TryGetValue(typeof(T), out var configurator))
                return (FilterTypeConfigurator<T>)configurator;
            var result = new FilterTypeConfigurator<T>();
            _types[typeof(T)] = result;
            return result;
        }

        public Filter Build()
        {
            return new Filter(_defaultValue, _types.Select(s => new { type = s.Key, ft = CreateFilter(s.Value) }).ToDictionary(s => s.type, s => s.ft));
        }

        private ITypeFilter CreateFilter(FilterTypeConfigurator value)
        {
            if (value.IsDeny)
                return new DenyTypeFilter();
            else if (value.AllowedProperties.Count > 0)
                return new AllowPropertyFilter(value.AllowedProperties, value.IsAllowed);
            else
                return new DenyPropertyFilter(value.DeniedProperties, value.IsAllowed);
        }
    }

    public class FilterTypeConfigurator
    {
        public HashSet<string> DeniedProperties { get; } = new HashSet<string>();

        public HashSet<string> AllowedProperties { get; } = new HashSet<string>();

        public Func<object, bool> IsAllowed { get; set; } = _ => true;

        public bool IsDeny { get; set; }
    }

    public class FilterTypeConfigurator<T> : FilterTypeConfigurator
    {
        public FilterTypeConfigurator<T> Allow<S>(Expression<Func<T, S>> getter)
        {
            AllowedProperties.Add(GetMemberFromExpression(getter));
            return this;
        }

        public FilterTypeConfigurator<T> Deny<S>(Expression<Func<T, S>> getter)
        {
            DeniedProperties.Add(GetMemberFromExpression(getter));
            return this;
        }

        public FilterTypeConfigurator<T> ConditionalObject(Func<T, bool> isAllowed)
        {
            IsAllowed = s => isAllowed((T)s);
            return this;
        }


        private string GetMemberFromExpression<S>(Expression<Func<T, S>> getter)
        {
            var memberExpression = (MemberExpression)getter.Body;
            return memberExpression.Member.Name;
        }
    }

}
