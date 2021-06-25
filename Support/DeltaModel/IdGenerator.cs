using MongoDB.Bson;
using System.Collections.Generic;
using System.Linq;

namespace Clutch.DeltaModel
{
    public static class IdGenerator
    {
        public static string New() => ObjectId.GenerateNewId().ToString();

        public static string NewShortId<T>(IDictionary<string, T> exclusions) => Enumerable.Range(0, int.MaxValue).Select(s => s.ToString()).First(s => !exclusions.ContainsKey(s));
    }
}
