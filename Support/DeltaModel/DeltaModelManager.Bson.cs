using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using System;

namespace Clutch.DeltaModel
{
    public partial class DeltaModelManager
    {
        private static NoDiscriminatorConvention NoDiscriminatorConventionInstance = new NoDiscriminatorConvention();

        public static void RegisterDiscriminatorConvention(Type type)
        {
            BsonSerializer.RegisterDiscriminatorConvention(type, NoDiscriminatorConventionInstance);
        }

        public BsonDocument SerializeBson(object obj, Filter filter)
        {
            var result = new BsonDocument();
            using (var writer = new BsonDocumentWriter(result))
            {
                var context = new BsonSerializingContext { Filter = filter, BsonWriter = writer };
                lock (this)
                {
                    SerializeBsonValue(obj, context);
                }
            }
            return result;
        }

        public void SerializeBsonValue(object obj, BsonSerializingContext context)
        {
            if (obj == null)
            {
                context.BsonWriter.WriteNull();
            }
            else if (obj is IEntityHandlerProvider provider)
            {
                provider.GetHandler().SerializeBson(context);
            }
            else
            {
                var s = BsonSerializer.LookupDiscriminatorConvention(obj.GetType());
                BsonSerializer.Serialize(context.BsonWriter, obj.GetType(), obj);
            }
        }

        public T DeserializeBson<T>(BsonDocument document)
        {
            using (var reader = new BsonDocumentReader(document))
            {
                var context = new BsonDeserializingContext { BsonReader = reader };
                return (T)DeserializeBsonValue(_configurations[typeof(T)], null, null, context);
            }
        }

        public object DeserializeBsonValue(TypeConfiguration typeConfig, IEntityHandler parent, string property, BsonDeserializingContext context)
        {
            if (typeConfig.Creator != null)
            {
                var result = typeConfig.Creator(parent, property);
                var handler = (IEntityHandlerProvider)result;
                handler.GetHandler().DeserializeBson(context);
                return result;
            }
            else
            {
                return BsonSerializer.Deserialize(context.BsonReader, typeConfig.Type);
            }
        }
    }

    public class BsonSerializingContext
    {
        public Filter Filter { get; set; }

        public IBsonWriter BsonWriter { get; set; }
    }

    public class BsonDeserializingContext
    {
        public IBsonReader BsonReader { get; set; }
    }

    public class NoDiscriminatorConvention : IDiscriminatorConvention
    {
        public string ElementName => null;

        public Type GetActualType(IBsonReader bsonReader, Type nominalType) => nominalType;

        public BsonValue GetDiscriminator(Type nominalType, Type actualType) => null;
    }
}
