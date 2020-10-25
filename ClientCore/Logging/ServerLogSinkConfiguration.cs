using Serilog;
using Serilog.Configuration;
using Serilog.Sinks.PeriodicBatching;
using Streamster.ClientCore.Services;
using System;

namespace Streamster.ClientCore.Logging
{
    public static class ServerLogSinkConfiguration
    {
        public static LoggerConfiguration ServerLog(this LoggerSinkConfiguration loggerSinkConfiguration, Func<HubConnectionService> hubConnectionServiceFactory)
        {
            var sink = new ServerLogSink(hubConnectionServiceFactory);

            var batchingOptions = new PeriodicBatchingSinkOptions
            {
                BatchSizeLimit = 100,
                Period = TimeSpan.FromSeconds(1),
                EagerlyEmitFirstEvent = true,
                QueueLimit = 1000
            };

            var batchingSink = new PeriodicBatchingSink(sink, batchingOptions);

            return loggerSinkConfiguration.Sink(batchingSink);
        }
    }
}
