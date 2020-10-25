using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Clutch.DeltaModel
{
    public class DeserializingContext : IDisposable
    {
        private TextReader _textReader;

        public JsonReader Reader { get; private set; }

        public JsonSerializer Serializer { get; private set; }

        public static DeserializingContext FromString(string content, JsonSerializer serializer)
        {
            var result = new DeserializingContext();
            result._textReader = new StringReader(content);
            result.Reader = new JsonTextReader(result._textReader);
            result.Serializer = serializer;
            return result;
        }

        public void Dispose()
        {
            Reader.Close();
            _textReader.Dispose();
        }
    }

    public class SerializingContext : IDisposable
    {
        public StringWriter StringWriter { get; private set; }

        public JsonWriter Writer { get; private set; }

        public JsonSerializer Serializer { get; private set; }

        public Filter Filter { get; private set; }

        public static SerializingContext Create(JsonSerializer serializer, Filter filter)
        {
            var result = new SerializingContext();
            result.StringWriter = new StringWriter();
            result.Writer = new JsonTextWriter(result.StringWriter);
            // result.Writer.Formatting = Formatting.Indented;
            result.Writer.Formatting = Formatting.None;
            result.Serializer = serializer;
            result.Filter = filter;
            return result;
        }

        public void Dispose()
        {
            Writer.Close();
            StringWriter.Dispose();
        }
    }
}
