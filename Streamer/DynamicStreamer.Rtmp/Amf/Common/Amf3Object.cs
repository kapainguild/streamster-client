using Harmonic.Networking.Amf.Data;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Harmonic.Networking.Amf.Common
{
    public class AmfObject : IDynamicObject, IEnumerable
    {
        private Dictionary<string, object> _fields = new Dictionary<string, object>();

        private Dictionary<string, object> _dynamicFields = new Dictionary<string, object>();

        public bool IsAnonymous { get => GetType() == typeof(AmfObject); }
        public bool IsDynamic { get => _dynamicFields.Any(); }

        public IReadOnlyDictionary<string, object> DynamicFields { get => _dynamicFields; }

        public IReadOnlyDictionary<string, object> Fields { get => _fields; }

        public Dictionary<string, object> FieldsDictionary 
        { 
            get 
            {
                var dic = new Dictionary<string, object>(_fields);
                foreach(var item in _dynamicFields)
                {
                    if (dic.ContainsKey(item.Key))
                        Log.Warning($"Dict already contains key ({item.Key}={item.Value})");
                    else
                        dic[item.Key] = item.Value;
                }
                return dic;
            } 
        }

        public AmfObject()
        {

        }

        public AmfObject(Dictionary<string, object> values)
        {
            _fields = values;
        }

        public void Add(string memberName, object member)
        {
            _fields.Add(memberName, member);
        }

        public void AddDynamic(string memberName, object member)
        {
            _dynamicFields.Add(memberName, member);
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable)Fields).GetEnumerator();
        }

        public override string ToString() => _dynamicFields.Count > 0 ? 
            "AmfObject(" + String.Join(", ", _fields.Select(s => $"{s.Key}=={s.Value}")) + "/dynamic:" + String.Join(", ", _dynamicFields.Select(s => $"{s.Key}=={s.Value}")) + ")" :
            "AmfObject(" + String.Join(", ", _fields.Select(s => $"{s.Key}=={s.Value}")) + ")";
    }
}
