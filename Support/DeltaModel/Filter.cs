using System;
using System.Collections.Generic;

namespace Clutch.DeltaModel
{
    public class Filter
    {
        private Dictionary<Type, ITypeFilter> _typeFilters;
        private bool _defaultValue;

        public Filter(bool defaultValue, Dictionary<Type, ITypeFilter> dictionary)
        {
            _defaultValue = defaultValue;
            _typeFilters = dictionary;
        }

        public bool IsOk(object obj, Type objType, string property)
        {
            if (_typeFilters.TryGetValue(objType, out var typeFilter))
                return typeFilter.IsOk(obj, property);
            return _defaultValue;
        }
    }

    public interface ITypeFilter
    {
        bool IsOk(object obj, string property);
    }

    public class DenyTypeFilter : ITypeFilter
    {
        public bool IsOk(object obj, string property) => false;
    }

    public class DenyPropertyFilter : ITypeFilter
    {
        private HashSet<string> _deniedProperties;
        private readonly Func<object, bool> _isAllowed;

        public DenyPropertyFilter(HashSet<string> deniedProperties, Func<object, bool> isAllowed)
        {
            _deniedProperties = deniedProperties;
            _isAllowed = isAllowed;
        }

        public bool IsOk(object obj, string property) => property == null ? _isAllowed(obj) : !_deniedProperties.Contains(property);
    }

    public class AllowPropertyFilter : ITypeFilter
    {
        private HashSet<string> _allowedProperties;
        private readonly Func<object, bool> _isAllowed;

        public AllowPropertyFilter(HashSet<string> allowedProperties, Func<object, bool> isAllowed)
        {
            _allowedProperties = allowedProperties;
            _isAllowed = isAllowed;
        }

        public bool IsOk(object obj, string property) => property == null ? _isAllowed(obj) : _allowedProperties.Contains(property);
    }
}
