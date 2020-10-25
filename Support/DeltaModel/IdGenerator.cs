using MongoDB.Bson;

namespace Clutch.DeltaModel
{
    public static class IdGenerator
    {
        public static string New() => ObjectId.GenerateNewId().ToString();
    }
}
