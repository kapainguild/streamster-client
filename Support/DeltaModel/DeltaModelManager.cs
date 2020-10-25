using Castle.DynamicProxy;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Clutch.DeltaModel
{
    public partial class DeltaModelManager
    {
        private static ProxyGenerator s_proxyGenerator = new ProxyGenerator();

        private Dictionary<Type, TypeConfiguration> _configurations;
        private readonly ILockerProvider _lockerProvider;
        protected IEntityHandler _rootHandler;
        protected object _root;
        private readonly JsonSerializer _serializer = new JsonSerializer();
        private List<ModelClient> _clients = new List<ModelClient>();

        public event EventHandler RootChanged;

        public SubscriptionManager Subscriptions { get; }

        public IDeltaServiceProvider DeltaServiceProvider { get; }

        public DeltaModelManager(Dictionary<Type, TypeConfiguration> configurations, IDeltaServiceProvider deltaServiceProvider, ILockerProvider lockerProvider)
        {
            _configurations = configurations;
            DeltaServiceProvider = deltaServiceProvider;
            _lockerProvider = lockerProvider;
            foreach (var type in configurations.Values)
            {
                if (!type.IsExternalType)
                {
                    if (type.ValueType != null)
                    {
                        type.Creator = (parent, property) =>
                        {
                            var t = typeof(DeltaModelDictionary<,>).MakeGenericType(type.KeyType.Type, type.ValueType.Type);
                            return Activator.CreateInstance(t, parent, property, this, type);
                        };
                    }
                    else
                    {
                        type.Creator = (parent, property) => Create(type.Type, parent, property);
                    }
                }
            }

            Subscriptions = new SubscriptionManager(this);
        }

        public IDisposable Lock() => _lockerProvider.GetLocker();

        public T GetLocal<T>(object model)
        {
            var local = (ILocalHolder)model;
            return (T)local.Local;
        }

        public T Create<T>()
        {
            return (T)Create(typeof(T), null, null);
        }

        public T GetOrCreate<T>(Func<T> getter, Action<T> init)
        {
            var n = getter();
            if (Equals(n, default(T)))
            {
                n = Create<T>();
                init(n);
            }
            return n;
        }

        public string GetId(object obj)
        {
            if (obj is IEntityHandlerProvider provider)
            {
                return provider.GetHandler().NameInParent;
            }
            throw new InvalidOperationException($"Cannot get id for {obj} ({obj == null})");
        }

        public T GetParent<T>(object obj)
        {
            if (obj is IEntityHandlerProvider provider)
            {
                return (T)provider.GetHandler().Parent.Proxy;
            }
            throw new InvalidOperationException($"Cannot get id for {obj} ({obj == null})");
        }

        public bool IsRooted(object obj)
        {
            if (obj is IEntityHandlerProvider provider)
            {
                return IsRooted(provider.GetHandler());
            }
            throw new InvalidOperationException($"Cannot get id for {obj} ({obj == null})");
        }

        public void Register(ModelClient client)
        {
            lock (this)
            {
                client.Manager = this;
                _clients.Add(client);
                client.AddChange(new Change
                {
                    Path = "/",
                    Type = ChangeType.Replace,
                    Serialized = Serialize(_root, client.Filter)
                });
            }
        }

        public void Unregister(ModelClient client)
        {
            lock (this)
            {
                _clients.Remove(client);
            }
        }

        protected object Create(Type type, IEntityHandler parent, string property)
        {
            var config = _configurations[type];
            var interceptor = new DeltaModelInterceptor(config, this, parent, property);

            var interfaces = new List<Type> { typeof(INotifyPropertyChanged), typeof(IEntityHandlerProvider) };
            if (config.LocalCreator != null)
                interfaces.Add(typeof(ILocalHolder));

            var result = s_proxyGenerator.CreateInterfaceProxyWithoutTarget(type,
                                                                        interfaces.ToArray(),
                                                                        interceptor);
            interceptor.Proxy = result;
            return result;
        }

        internal void ChangeAdd(IEntityHandler parent, string key, object value)
        {
            lock (this)
            {
                SetObjectParent(value, key, parent);

                if (IsRooted(parent))
                {
                    AddChange(ChangeType.Add, parent, key, value);
                    AttachNewItem(null, value);
                }
            }
        }

        internal void ChangeRemove(IEntityHandler parent, string key, object oldValue)
        {
            lock (this)
            {
                SetObjectParent(oldValue, null, null);
                if (IsRooted(parent))
                {
                    AddChange(ChangeType.Remove, parent, key, null);
                    DetachOldItem(null, oldValue);
                }
            }
        }

        internal void ChangeReplace(IEntityHandler parent, string key, object value, object oldValue)
        {
            lock (this)
            {
                SetObjectParent(value, key, parent);
                SetObjectParent(oldValue, null, null);

                if (IsRooted(parent))
                {
                    AddChange(ChangeType.Replace, parent, key, value);
                    DetachOldItem(null, oldValue);
                    AttachNewItem(null, value);
                }
            }
        }

        private void AddChange(ChangeType type, IEntityHandler parent, string key, object value)
        {
            string path = GetPath(parent, key);
            Subscriptions.NotifyChange(null, type, parent, key);
            _clients.ForEach(client => AddChange(client, type, path, parent, key, value));
        }

        

        private void AddChange(ModelClient client, ChangeType type, string path, IEntityHandler parent, string key, object value)
        {
            if (CheckFilterToRoot(parent, key, client.Filter))
                client.AddChange(new Change 
                {
                    Type = type, 
                    Path = path,
                    Serialized = type == ChangeType.Remove ? null : Serialize(value, client.Filter)
                });
        }

        private string GetPath(IEntityHandler handler, string key)
        {
            LinkedList<string> result = new LinkedList<string>();
            result.AddFirst(key);

            while (handler != null)
            {
                key = handler.NameInParent;
                result.AddFirst(key);
                handler = handler.Parent;
            }

            return string.Join("/", result);
        }

        private bool CheckFilterToRoot(IEntityHandler handler, string key, Filter filter)
        {
            if (handler == null)
                return true;
            do
            {
                if (!filter.IsOk(handler.Proxy, handler.ProxyType, key))
                    return false;

                key = handler.NameInParent;
                handler = handler.Parent;
            }
            while (handler != null);

            return true;
        }

        private void SetObjectParent(object obj, string key, IEntityHandler newParent)
        {
            if (obj is IEntityHandlerProvider provider)
            {
                var handler = provider.GetHandler();
                handler.Parent = newParent;
                if (key != null) //we want to keep to get id after object is detached
                    handler.NameInParent = key; 
            }
        }

        private bool IsRooted(IEntityHandler handler)
        {
            while (handler != null)
            {
                if (handler == _rootHandler)
                    return true;
                handler = handler.Parent;
            }
            return false;
        }

        public string Serialize(object obj, Filter filter)
        {
            using (var context = SerializingContext.Create(_serializer, filter))
            {
                SerializeMemberValue(obj, context);
                return context.StringWriter.ToString();
            }
        }

        public void SerializeMemberValue(object memberValue, SerializingContext context)
        {
            if (memberValue is IEntityHandlerProvider provider)
                provider.GetHandler().Serialize(context);
            else
                context.Serializer.Serialize(context.Writer, memberValue);
        }

        public object Deserialize(Type type, string str)
        {
            using (var context = DeserializingContext.FromString(str, _serializer))
            {
                context.Reader.Read();
                return DeserializeMemeberValue(_configurations[type], null, null, context);
            }
        }

        public object DeserializeMemeberValue(TypeConfiguration typeConfig, IEntityHandler parent, string property, DeserializingContext context)
        {
            if (typeConfig.IsExternalType)
            {
                return context.Serializer.Deserialize(context.Reader, typeConfig.Type);
            }
            else if (typeConfig.Creator != null)
            {
                var result = typeConfig.Creator(parent, property);
                var handler = (IEntityHandlerProvider)result;
                handler.GetHandler().Deserialize(context);
                return result;
            }
            else
                throw new Exception();
        }

        public void DeserializeIgnoredValue(DeserializingContext context)
        {
            var res = context.Serializer.Deserialize(context.Reader);
            Log.Warning($"Deserializing ignored value '{res}'");
        }

        public List<Change> ApplyChanges(ModelClient sourceClient, string changesData)
        {
            lock (this)
            {
                var otherClients = _clients.Where(s => s != sourceClient).ToList();
                List<Change> changes = new List<Change>();
                using (var context = DeserializingContext.FromString(changesData, _serializer))
                {
                    context.Reader.Read();
                    while (context.Reader.Read() && context.Reader.TokenType != JsonToken.EndArray)
                    {
                        var change = DeserializeAndApplyChange(context);
                        if (!change.Ignored) // ignored change due to unsync
                        {
                            Subscriptions.NotifyChange(sourceClient, change.Type, change.Handler, change.Key);
                            if (change.OldValue != null)
                                DetachOldItem(sourceClient, change.OldValue);
                            if (change.Value != null)
                                AttachNewItem(sourceClient, change.Value);
                            otherClients.ForEach(client => AddChange(client, change.Type, change.Path, change.Handler, change.Key, change.Value));
                            changes.Add(change);
                        }
                    }
                }
                return changes;
            }
        }

        private Change DeserializeAndApplyChange(DeserializingContext context)
        {
            var result = new Change();
            while (context.Reader.Read() && context.Reader.TokenType != JsonToken.EndObject)
            {
                if (!(context.Reader.Value is string))
                    Log.Error($"Unexpected serialization '{context.Reader.Value?.GetType()}=context.Reader.Value'");
                string property = (string)context.Reader.Value;
                context.Reader.Read();

                switch(property)
                {
                    case "op":
                        result.Type = (ChangeType)Enum.Parse(typeof(ChangeType), (string)context.Reader.Value, true);
                        break;

                    case "path":
                        result.Path = (string)context.Reader.Value;
                        if (result.Type == ChangeType.Remove)
                            DeserializeAndApplyChangeValue(result, context);
                        break;

                    case "value":
                        DeserializeAndApplyChangeValue(result, context);
                        break;
                }
            }
            return result;
        }

        public object DeserializeAndApplyAdd(IEntityHandler parent, string key, TypeConfiguration typeConfig, Change change, DeserializingContext context)
        {
            change.Value = DeserializeMemeberValue(typeConfig, parent, key, context);
            return change.Value;
        }

        public void DeserializeAndApplyRemove(object oldValue, Change change)
        {
            change.OldValue = oldValue;
            SetObjectParent(oldValue, null, null);
        }

        public object DeserializeAndApplyReplace(IEntityHandler parent, string key, object oldValue, TypeConfiguration typeConfig, Change change, DeserializingContext context)
        {
            change.OldValue = oldValue;
            SetObjectParent(oldValue, null, null);
            change.Value = DeserializeMemeberValue(typeConfig, parent, key, context);
            return change.Value;
        }

        private void AttachNewItem(ModelClient sourceClient, object value)
        {
            Visit(value, (e) =>
            {
                if (e is IEntityHandlerProvider provider)
                {
                    var handler = provider.GetHandler();
                    if (handler is DeltaModelInterceptor interceptor)
                        interceptor.InitLocal();

                    Subscriptions.NotifyAttached(sourceClient, handler);
                }
            });
        }

        private void DetachOldItem(ModelClient sourceClient, object value)
        {
            Visit(value, (e) =>
            {
                if (e is IEntityHandlerProvider provider)
                {
                    var handler = provider.GetHandler();
                    if (handler is DeltaModelInterceptor interceptor)
                        interceptor.UninitLocal();

                    Subscriptions.NotifyDetached(sourceClient, handler);
                }
            });
        }

        private void RootChange(Change change, DeserializingContext context)
        {
            _root = DeserializeAndApplyReplace(null, "", _root, _configurations[_rootHandler.ProxyType], change, context);
            _rootHandler = ((IEntityHandlerProvider)_root).GetHandler();

            RootChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DeserializeAndApplyChangeValue(Change change, DeserializingContext context)
        {
            var path = change.Path.Split('/');
            if (path[1].Length == 0)
            {
                RootChange(change, context);
                return;
            }
            var handler = _rootHandler;

            for(int q = 1; q < path.Length - 1; q++)
            {
                if (handler.TryGetValue(path[q], out var obj))
                {
                    handler = ((IEntityHandlerProvider)obj).GetHandler();
                }
                else
                {
                    change.Ignored = true;
                    DeserializeIgnoredValue(context);
                    Log.Warning($"Change '{change.Path}:{change.Type}' is ignored");
                    return;
                }
            }

            change.Key = path[path.Length - 1];
            handler.DeserializeAndApplyChangeValue(change, context);
            change.Handler = handler;
        }

        private void Visit(object value, Action<object> onVisit)
        {
            if (value is IEntityHandlerProvider provider)
            {
                var handler = provider.GetHandler();
                foreach (var child in handler.GetValues().Select(s => s.Value))
                {
                    Visit(child, onVisit);
                }
            }
            onVisit(value);
        }

        public bool IsDeepEqual(object obj1, object obj2)
        {
            if (obj1 is IEntityHandlerProvider handler1)
            {
                return handler1.GetHandler().IsDeepEqual(obj2);
            }
            else if (obj1 != null)
            {
                return obj1.Equals(obj2);
            }
            else return obj2 == null;
        }
    }

    public class DeltaModelManager<TRoot> : DeltaModelManager
    {
        public TRoot Root 
        { 
            get => (TRoot)_root;
            set 
            {
                _root = value;
                _rootHandler = ((IEntityHandlerProvider)_root).GetHandler();
            }
        }

        public DeltaModelManager(Dictionary<Type, TypeConfiguration> configurations, IDeltaServiceProvider deltaServiceProvider, ILockerProvider locker) : base (configurations, deltaServiceProvider, locker)
        {
            Root = (TRoot)Create(typeof(TRoot), null, null);
            _rootHandler = ((IEntityHandlerProvider)Root).GetHandler();
        }
    }
}
