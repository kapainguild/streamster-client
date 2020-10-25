using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Clutch.DeltaModel
{
    public class SubscriptionManager
    {
        private Dictionary<Type, TypeSubscriptions> _types = new Dictionary<Type, TypeSubscriptions>();
        private readonly DeltaModelManager _manager;

        private List<Action> _notifications = new List<Action>();
        private bool _onChangeCalled;
        private bool _onLocalChangeCalled;

        public SubscriptionManager(DeltaModelManager manager)
        {
            _manager = manager;
        }

        public Action OnChangeForSubscriptions { get; set; } = () => { };

        public Action OnLocalChange { get; set; } = () => { };


        public List<Action> GetAndClearNotifications()
        {
            lock(_manager)
            {
                var notifications = _notifications;
                _notifications = new List<Action>();
                _onChangeCalled = false;
                return notifications;
            }
        }

        public Subscription SubscribeForType<T>(Action<T, ChangeType> action)
        {
            var t = GetOrCreate(typeof(T));
            Action<object, ChangeType> ac = (o, c) => action((T)o, c);

            t.TypeChanges.Add(ac);

            return new Subscription
            {
                Unsubscribe = () => t.TypeChanges.Remove(ac)
            };
        }

        public Subscription SubscribeForAnyProperty<T>(Action<T, ChangeType> action)
        {
            var t = GetOrCreate(typeof(T));
            Action<object, ChangeType> ac = (o, c) => action((T)o, c);

            t.AnyPropertyChanges.Add(ac);

            return new Subscription
            {
                Unsubscribe = () => t.AnyPropertyChanges.Remove(ac)
            };
        }

        public Subscription SubscribeForProperties<T>(Expression<Func<T, object>> getter, Action<T, ChangeType, string> action)
        {
            return SubscribeForProperties(action, GetMemberFromExpression(getter));
        }

        private string GetMemberFromExpression<T>(Expression<Func<T, object>> getter)
        {
            if (getter.Body is UnaryExpression unary)
            {
                var memberExpression = (MemberExpression)unary.Operand;
                return memberExpression.Member.Name;
            }
            else
            {
                var property = (MemberExpression)getter.Body;
                return property.Member.Name;
            }
        }

        public Subscription SubscribeForProperties<T>(string propertyName, Action<T, ChangeType, string> action)
        {
            return SubscribeForProperties(action, propertyName);
        }

        public Subscription SubscribeForProperties<T>(string[] properties, Action<T, ChangeType, string> action)
        {
            return SubscribeForProperties(action, properties);
        }

        public Subscription SubscribeForProperties<T>(Action<T, ChangeType, string> action, params string[] names)
        {
            var t = GetOrCreate(typeof(T));

            List<List<Action<object, ChangeType, string>>> lists = new List<List<Action<object, ChangeType, string>>>();
            Action<object, ChangeType, string> ac = (o, c, p) => action((T)o, c, p);
            foreach (var name in names)
            {
                var list = GetOrCreate(t, name);
                list.Add(ac);
                lists.Add(list);
            }

            return new Subscription
            {
                Unsubscribe = () => lists.ForEach(l => l.Remove(ac))
            };
        }

        private TypeSubscriptions GetOrCreate(Type type)
        {
            if (!_types.TryGetValue(type, out var result))
            {
                result = new TypeSubscriptions();
                _types[type] = result;
            }
            return result;
        }

        private List<Action<object, ChangeType, string>> GetOrCreate(TypeSubscriptions type, string property)
        {
            if (!type.PropertySubscriptions.TryGetValue(property, out var result))
            {
                result = new List<Action<object, ChangeType, string>>();
                type.PropertySubscriptions[property] = result;
            }
            return result;
        }

        public void ResetLocalChangesFlag()
        {
            lock (_manager)
                _onLocalChangeCalled = false;
        }

        public void NotifyChange(ModelClient sourceClient, ChangeType type, IEntityHandler parent, string key)
        {
            if (parent != null && _types.TryGetValue(parent.ProxyType, out var subscriptions))
            {
                subscriptions.AnyPropertyChanges.ForEach(s => AddCall(() => s(parent.Proxy, type)));

                if (subscriptions.PropertySubscriptions.TryGetValue(key, out var perProperty))
                {
                    perProperty.ForEach(p => AddCall(() => p(parent.Proxy, type, key)));
                }
            }
            NotifyLocalChange(sourceClient);
        }

        private void AddCall(Action action)
        {
            _notifications.Add(action);
            if (!_onChangeCalled)
            {
                _onChangeCalled = true;
                OnChangeForSubscriptions();
            }
        }

        private void AttachDetach(ModelClient sourceClient, IEntityHandler handler, ChangeType type)
        {
            if (_types.TryGetValue(handler.ProxyType, out var subscriptions))
            {
                subscriptions.TypeChanges.ForEach(s => AddCall(() => s(handler.Proxy, type)));
                subscriptions.AnyPropertyChanges.ForEach(s => AddCall(() => s(handler.Proxy, type)));

                var props = subscriptions.PropertySubscriptions;
                if (props.Count > 0)
                {
                    var children = handler.GetValues();
                    foreach(var child in children)
                    {
                        if (props.TryGetValue(child.Key, out var perProperty))
                        {
                            perProperty.ForEach(p => AddCall(() => p(handler.Proxy, type, child.Key)));
                        }
                    }
                }
            }

            NotifyLocalChange(sourceClient);
        }

        private void NotifyLocalChange(ModelClient sourceClient)
        {
            if (sourceClient == null && !_onLocalChangeCalled)
            {
                _onLocalChangeCalled = true;
                OnLocalChange();
            }
        }

        public void NotifyAttached(ModelClient sourceClient, IEntityHandler handler)
        {
            AttachDetach(sourceClient, handler, ChangeType.Add);
        }

        public void NotifyDetached(ModelClient sourceClient, IEntityHandler handler)
        {
            AttachDetach(sourceClient, handler, ChangeType.Remove);
        }
    }

    class TypeSubscriptions
    {
        public List<Action<object, ChangeType>> TypeChanges { get; } = new List<Action<object, ChangeType>>();

        public List<Action<object, ChangeType>> AnyPropertyChanges { get; } = new List<Action<object, ChangeType>>();

        public Dictionary<string, List<Action<object, ChangeType, string>>> PropertySubscriptions { get; } = new Dictionary<string, List<Action<object, ChangeType, string>>>();
    }

    public class Subscription
    {
        public Action Unsubscribe { get; internal set; }
    }
}
