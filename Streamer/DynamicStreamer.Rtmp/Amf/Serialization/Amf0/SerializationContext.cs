﻿using Harmonic.Buffers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Amf.Serialization.Amf0
{
    public class SerializationContext : IDisposable
    {
        public ByteBuffer Buffer { get; private set; }
        public List<object> ReferenceTable { get; set; } = new List<object>();

        public int MessageLength => Buffer.Length;

        private bool _disposeBuffer = true;

        public SerializationContext()
        {
            Buffer = new ByteBuffer();
        }

        public SerializationContext(ByteBuffer buffer)
        {
            Buffer = buffer;
            _disposeBuffer = false;
        }

        public void GetMessage(Span<byte> buffer)
        {
            ReferenceTable.Clear();
            Buffer.TakeOutMemory(buffer);
        }

        public void Dispose()
        {
            if (_disposeBuffer)
            {
                ((IDisposable)Buffer).Dispose();
            }
        }
    }
}
