using MongoDB.Bson.IO;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace Clutch.DeltaModel
{
    class DeltaModelDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDeltaModelDictionary
    {
        private readonly DeltaModelManager _manager;
        private readonly TypeConfiguration _typeConfig;
        private Dictionary<string, object> _inner = new Dictionary<string, object>();

        private ObservableCollection<TValue> _observable = null;

        public DeltaModelDictionary(IEntityHandler parent, string property, DeltaModelManager manager, TypeConfiguration configuration)
        {
            _manager = manager;
            _typeConfig = configuration;
            Parent = parent;
            NameInParent = property;
        }

        public string NameInParent { get; set; }

        public IEntityHandler GetHandler()
        {
            return this;
        }

        public object Proxy => this;

        public Type ProxyType => _typeConfig.Type;

        public bool TryGetValue(string property, out object value) => _inner.TryGetValue(property, out value);

        public IEnumerable<KeyValuePair<string, object>> GetValues() => _inner;

        public bool IsDeepEqual(object target)
        {
            if (target is DeltaModelDictionary<TKey, TValue> buddy)
            {
                if (buddy._inner.Count != _inner.Count)
                    return false;

                foreach(var s in _inner)
                {
                    if (!buddy._inner.TryGetValue(s.Key, out var buddyValue))
                        return false;

                    if (!_manager.IsDeepEqual(s.Value, buddyValue))
                        return false;
                }
                return true;
            }
            return false;
        }

        public void DeserializeAndApplyChangeValue(Change change, DeserializingContext context)
        {
            var property = change.Key;
            if (_inner.TryGetValue(property, out var oldValue))
            {
                switch (change.Type)
                {
                    case ChangeType.Add: 
                        Log.Warning($"Unsync while adding key {property} to dictionary");
                        _manager.DeserializeIgnoredValue(context);
                        break;
                    case ChangeType.Remove:
                        _manager.DeserializeAndApplyRemove(oldValue, change);
                        InternalRemove(property, oldValue);
                        break;
                    case ChangeType.Replace:
                        InternalReplace(property, oldValue, _manager.DeserializeAndApplyReplace(this, property, oldValue, _typeConfig.ValueType, change, context));
                        break;
                }
            }
            else
            {
                if (change.Type == ChangeType.Add)
                {
                    InternalAdd(property, _manager.DeserializeAndApplyAdd(this, property, _typeConfig.ValueType, change, context));
                }
                else
                {
                    Log.Warning($"Unsync while '{change}' key {property} to dictionary");
                    if (change.Type == ChangeType.Replace)
                        _manager.DeserializeIgnoredValue(context);
                }
            }
        }

        private void InternalAdd(string property, object newValue)
        {
            _inner.Add(property, newValue);
            _observable?.Add((TValue)newValue);
        }

        private void InternalReplace(string propertyPath, object oldValue, object newValue)
        {
            _inner[propertyPath] = oldValue;
            if (_observable != null)
            {
                int idx = _observable.IndexOf((TValue)oldValue);
                _observable[idx] = (TValue)newValue;
            }
        }

        private void InternalRemove(string propertyPath, object oldValue)
        {
            _inner.Remove(propertyPath);
            _observable?.Remove((TValue)oldValue);
        }

        public void Serialize(SerializingContext context)
        {
            context.Writer.WriteStartObject();
            foreach (var item in _inner)
            {
                if (context.Filter.IsOk(item.Value, _typeConfig.ValueType.Type, null))
                {
                    context.Writer.WritePropertyName(item.Key);
                    _manager.SerializeMemberValue(item.Value, context);
                }
            }
            context.Writer.WriteEndObject();
        }

        public void SerializeBson(BsonSerializingContext context)
        {
            context.BsonWriter.WriteStartDocument();
            foreach (var item in _inner)
            {
                if (string.IsNullOrEmpty(item.Key))
                {
                    Log.Warning($"Trying to bserialize empty value for Dictionary of {_typeConfig.ValueType.Type}");
                }
                else if (context.Filter.IsOk(item.Value, _typeConfig.ValueType.Type, null))
                {
                    context.BsonWriter.WriteName(item.Key);
                    _manager.SerializeBsonValue(item.Value, context);
                }
            }
            context.BsonWriter.WriteEndDocument();
        }

        public void Deserialize(DeserializingContext context)
        {
            while (context.Reader.Read() && context.Reader.TokenType != Newtonsoft.Json.JsonToken.EndObject)
            {
                string key = (string)context.Reader.Value;
                context.Reader.Read();
                var o = _manager.DeserializeMemeberValue(_typeConfig.ValueType, this, key, context);
                _inner.Add(key, o);
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

                var o = _manager.DeserializeBsonValue(_typeConfig.ValueType, this, name, context);
                _inner.Add(name, o);
                if (context.BsonReader.State == BsonReaderState.Type)
                    context.BsonReader.ReadBsonType();
            }
            context.BsonReader.ReadEndDocument();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IEntityHandler Parent { get; set; } = null;

        public void Add(TKey key, TValue value)
        {
            var property = key.ToString();
            using (_manager.Lock())
            {
                _manager.ChangeAdd(this, property, value);
                InternalAdd(property, value);
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                using (_manager.Lock())
                    return (TValue)_inner[key.ToString()];
            }
            set
            {
                var property = key.ToString();

                using (_manager.Lock())
                {
                    if (_inner.TryGetValue(property, out var oldValue))
                    {
                        if (value == null)
                        {
                            _manager.ChangeRemove(this, property, oldValue);
                            InternalRemove(property, oldValue);
                        }
                        else
                        {
                            _manager.ChangeReplace(this, property, value, oldValue);
                            InternalReplace(property, oldValue, value);
                        }
                    }
                    else
                    {
                        if (value != null)
                        {
                            _manager.ChangeAdd(this, property, value);
                            InternalAdd(property, value);
                        }
                    }
                }
            }
        }
        

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public bool Remove(TKey key)
        {
            var property = key.ToString();
            using (_manager.Lock())
            {
                if (_inner.TryGetValue(property, out var entry))
                {
                    _manager.ChangeRemove(this, property, entry);
                    InternalRemove(property, entry);
                    return true;
                }
            }
            return false;
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            using (_manager.Lock())
            {
                if (_inner.TryGetValue(key.ToString(), out var entry))
                {
                    value = (TValue)entry;
                    return true;
                }
            }
            value = default;
            return false;
        }

        public ICollection<TKey> Keys => _inner.Keys.OfType<TKey>().ToList();

        public ICollection<TValue> Values
        {
            get
            {
                using (_manager.Lock())
                {
                    if (_observable == null)
                        _observable = new ObservableCollection<TValue>(_inner.Values.OfType<TValue>());
                }
                return _observable;
            }
        }

        public int Count => _inner.Count;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsReadOnly => false;

        public bool Contains(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();

        public bool ContainsKey(TKey key)
        {
            using (_manager.Lock())
                return _inner.ContainsKey(key.ToString());
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => throw new NotSupportedException();

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            if (typeof(TKey) == typeof(string))
            {
                using (_manager.Lock())
                {
                    foreach (var item in _inner)
                    {
                        yield return new KeyValuePair<TKey, TValue>((TKey)(object)item.Key, (TValue)item.Value);
                    }
                }
            }
            else throw new NotSupportedException();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => $"[Count = {_inner.Count}]";
    }
}
