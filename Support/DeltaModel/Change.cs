using System.Collections.Generic;

namespace DeltaModel
{
    public class Change
    {
        public ChangeType Type { get; set; }

        public string Path { get; set; }

        public object Value { get; set; }

        public string Serialized { get; set; }

        public IEntityHandler Handler { get; set; }

        public string Key { get; set; }

        public object OldValue { get; set; }

        public bool Ignored { get; internal set; }

        public static string SerializeChanges(List<Change> changes)
        {
            using (var context = SerializingContext.Create(null, null))
            {
                var writer = context.Writer;

                writer.WriteStartArray();
                foreach (var change in changes)
                {
                    writer.WriteStartObject();

                    writer.WritePropertyName("op");
                    writer.WriteValue(change.Type.ToString().ToLowerInvariant());

                    writer.WritePropertyName("path");
                    writer.WriteValue(change.Path);

                    switch (change.Type)
                    {
                        case ChangeType.Add:
                        case ChangeType.Replace:
                            writer.WritePropertyName("value");
                            writer.WriteRawValue(change.Serialized);
                            break;
                    }
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                return context.StringWriter.ToString();
            }
        }
    }

    public enum ChangeType
    {
        Add,
        Remove,
        Replace
    }
}
