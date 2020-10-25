using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientData.Model
{
    public interface IIngest
    {
        IngestData Data { get; set; }
    }

    public class IngestData
    {
        public string Type { get; set; }

        public string Output { get; set; }

        public string Options { get; set; }
    }

    public interface IOutgest
    {
        OutgestData Data { get; set; }
    }

    public class OutgestData
    {
        public string Type { get; set; }

        public string Output { get; set; }

        public string Options { get; set; }
    }
}
