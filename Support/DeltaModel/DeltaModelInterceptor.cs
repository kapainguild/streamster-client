using Castle.DynamicProxy;
using MongoDB.Bson.IO;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Clutch.DeltaModel
{
    public class DeltaModelInterceptor : IInterceptor, IEntityHandler
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        private TypeConfiguration _typeConfig;
        private DeltaModelManager _manager;
        private object _local;
        private PropertyChangedEventHandler _propertyChangedHandler;

        public object Proxy { get; set; }

        public Type ProxyType => _typeConfig.Type;

        public IEntityHandler Parent { get; set; }

        public string NameInParent { get; set; }

        public DeltaModelInterceptor(TypeConfiguration configuration, DeltaModelManager manager, IEntityHandler parent, string property)
        {
            _typeConfig = configuration;
            _manager = manager;
            Parent = parent;
            NameInParent = property;

            foreach(var prop in configuration.Members)
            {
                if (prop.Value.TypeConfiguration.ValueType != null)
                {
                    _values[prop.Key] = prop.Value.TypeConfiguration.Creator(this, prop.Key);
                }

                if (prop.Value.DefaultValue != null)
                {
                    _values[prop.Key] = prop.Value.DefaultValue;
                }
            }
        }

        internal void InitLocal()
        {
            if (_typeConfig.LocalCreator != null)
            {
                _local = _typeConfig.LocalCreator.Invoke(Proxy, _manager);
                _propertyChangedHandler?.Invoke(Proxy, new PropertyChangedEventArgs(nameof(ILocalHolder.Local)));
            }
        }

        public void UninitLocal()
        {
            if (_local != null)
            {
                if (_local is IDisposable d)
                    d.Dispose();
                _local = null;
                _propertyChangedHandler?.Invoke(Proxy, new PropertyChangedEventArgs(nameof(ILocalHolder.Local)));
            }
        }

        public bool TryGetValue(string property, out object value)
        {
            return _values.TryGetValue(property, out value);
        }

        public IEnumerable<KeyValuePair<string, object>> GetValues() => _values;

        public void Intercept(IInvocation invocation)
        {
            var name = invocation.Method.Name;

            if (name == nameof(IEntityHandlerProvider.GetHandler))
            {
                invocation.ReturnValue = this;
            }
            else if (name.StartsWith("get_"))
            {
                using (_manager.Lock())
                {
                    var property = name.Substring(4);

                    if (property == nameof(ILocalHolder.Local))
                    {
                        invocation.ReturnValue = _local;
                    }
                    else
                    {
                        if (!_values.TryGetValue(property, out var entry))
                        {
                            entry = _typeConfig.Members[property].TypeConfiguration.DefaultValue;
                        }

                        invocation.ReturnValue = entry;
                    }
                }
            } 
            else if (name.StartsWith("set_"))
            {
                var property = name.Substring(4);

                using (_manager.Lock())
                {
                    var newValue = invocation.Arguments[0];
                    var memberConfig = _typeConfig.Members[property];

                    if (!_values.TryGetValue(property, out var value))
                    {
                        if (newValue != null)
                        {
                            // null => value
                            _values[property] = newValue;
                            _manager.ChangeAdd(this, property, newValue);
                        }
                        //else null => null
                    }
                    else
                    {
                        if (newValue == null)
                        {
                            // value => null
                            _values.Remove(property);
                            _manager.ChangeRemove(this, property, value);
                        }
                        else
                        {
                            //value => value
                            if (memberConfig.DontCompareBeforeSet || !Equals(value, newValue))
                            {
                                _values[property] = newValue;
                                _manager.ChangeReplace(this, property, newValue, value);
                            }
                        }
                    }
                }
                _propertyChangedHandler?.Invoke(invocation.Proxy, new PropertyChangedEventArgs(property));
            }
            else if (invocation.Method.Name == "add_PropertyChanged")
            {
                _propertyChangedHandler = (PropertyChangedEventHandler)Delegate.Combine(_propertyChangedHandler, (Delegate)invocation.Arguments[0]);
            }
            else if (invocation.Method.Name == "remove_PropertyChanged")
            {
                _propertyChangedHandler = (PropertyChangedEventHandler)Delegate.Remove(_propertyChangedHandler, (Delegate)invocation.Arguments[0]);
            }
        }

        public void DeserializeAndApplyChangeValue(Change change, DeserializingContext context)
        {
            var property = change.Key;
            if (_values.TryGetValue(property, out var value))
            {
                switch (change.Type)
                {
                    case ChangeType.Add: 
                        Log.Warning($"Unsync while adding property {property} to {ProxyType}");
                        _manager.DeserializeIgnoredValue(context);
                        break;
                    case ChangeType.Remove:
                        _manager.DeserializeAndApplyRemove(value, change);
                        _values.Remove(property);
                        _propertyChangedHandler?.Invoke(Proxy, new PropertyChangedEventArgs(property));
                        break;
                    case ChangeType.Replace:
                        _values[property] = _manager.DeserializeAndApplyReplace(this, property, value, _typeConfig.Members[property].TypeConfiguration, change, context);
                        _propertyChangedHandler?.Invoke(Proxy, new PropertyChangedEventArgs(property));
                        break;
                }
            }
            else
            {
                if (change.Type == ChangeType.Add)
                {
                    _values.Add(property, _manager.DeserializeAndApplyAdd(this, property, _typeConfig.Members[property].TypeConfiguration, change, context));
                    _propertyChangedHandler?.Invoke(Proxy, new PropertyChangedEventArgs(property));
                }
                else
                {
                    Log.Warning($"Unsync while '{change}' property {property} in {ProxyType}");
                    if (change.Type == ChangeType.Replace)
                        _manager.DeserializeIgnoredValue(context);
                }
            }
        }

        public void Deserialize(DeserializingContext context)
        {
            while (context.Reader.Read() && context.Reader.TokenType != Newtonsoft.Json.JsonToken.EndObject)
            {
                string property = (string)context.Reader.Value;
                var memberConfig = _typeConfig.Members[property];
                context.Reader.Read();
                _values[property] = _manager.DeserializeMemeberValue(memberConfig.TypeConfiguration, this, property, context); 
            }
        }

        public void DeserializeBson(BsonDeserializingContext context)
        {
            context.BsonReader.ReadStartDocument();
            if (context.BsonReader.State == BsonReaderState.Type)
                context.BsonReader.ReadBsonType();
            while (context.BsonReader.State != BsonReaderState.EndOfDocument)
            {
                string name = context.BsonReader.ReadName(Utf8NameDecoder.Instance);

                var o = _manager.DeserializeBsonValue(_typeConfig.Members[name].TypeConfiguration, this, name, context);
                _values[name] = o;
                if (context.BsonReader.State == BsonReaderState.Type)
                    context.BsonReader.ReadBsonType();
            }
            context.BsonReader.ReadEndDocument();
        }

        public void Serialize(SerializingContext context)
        {
            context.Writer.WriteStartObject();

            foreach (var member in _values)
            {
                if (context.Filter.IsOk(Proxy, _typeConfig.Type, member.Key))
                {
                    context.Writer.WritePropertyName(member.Key);
                    _manager.SerializeMemberValue(member.Value, context);
                }
            }
            context.Writer.WriteEndObject();
        }

        public void SerializeBson(BsonSerializingContext context)
        {
            context.BsonWriter.WriteStartDocument();
            foreach (var item in _values)
            {
                if (context.Filter.IsOk(Proxy, _typeConfig.Type, item.Key))
                {
                    context.BsonWriter.WriteName(item.Key);
                    _manager.SerializeBsonValue(item.Value, context);
                }
            }
            context.BsonWriter.WriteEndDocument();
        }

        public bool IsDeepEqual(object target)
        {
            if (target is IEntityHandlerProvider buddyProvider && 
                buddyProvider.GetHandler() is DeltaModelInterceptor buddy)
            {
                if (buddy._values.Count != _values.Count)
                    return false;

                foreach (var s in _values)
                {
                    if (!buddy._values.TryGetValue(s.Key, out var buddyValue))
                        return false;

                    if (!_manager.IsDeepEqual(s.Value, buddyValue))
                        return false;
                }
                return true;
            }
            return false;
        }
    }
}
