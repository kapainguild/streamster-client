using MongoDB.Bson;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Clutch.DeltaModel
{
    public interface IEntityHandler
    {
        IEntityHandler Parent { get; set; }

        object Proxy { get; }

        Type ProxyType { get; }

        string NameInParent { get; set; }

        void Deserialize(DeserializingContext context);

        void Serialize(SerializingContext context);

        bool TryGetValue(string property, out object value);

        IEnumerable<KeyValuePair<string, object>> GetValues();

        bool IsDeepEqual(object target);

        void DeserializeAndApplyChangeValue(Change result, DeserializingContext context);

        void SerializeBson(BsonSerializingContext conext);

        void DeserializeBson(BsonDeserializingContext context);
    }
}
