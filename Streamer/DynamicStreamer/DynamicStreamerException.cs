using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicStreamer
{
    public class DynamicStreamerException : Exception
    {
        public int ErrorCode { get; }

        public ErrorCodes AsErrorCodes => (ErrorCodes)ErrorCode;

        public DynamicStreamerException(string message, int errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        public DynamicStreamerException(string message) : this(message, (int)ErrorCodes.InternalErrorUnknown)
        {
        }
    }
}
