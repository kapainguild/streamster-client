using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.PeriodicBatching;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Logging
{
    class ServerLogSink : IBatchedLogEventSink
    {
        private readonly Func<HubConnectionService> _factory;
        private HubConnectionService _service;
        private CompactJsonFormatter _formatter = new CompactJsonFormatter();
        private Queue<string[]> _batches = new Queue<string[]>();

        public ServerLogSink(Func<HubConnectionService> hubConnectionServiceFactory)
        {
            _factory = hubConnectionServiceFactory;
        }

        public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
        {
            if (_service == null)
                _service = _factory();

            var asList = batch.Select(s => ChangeLevel(s)).ToList();

            var serialized = asList.Select(s => Serialize(s)).ToArray();
            _batches.Enqueue(serialized);
            while (_batches.Count > 100)
            {
                _batches.Dequeue();
            }

            if (_service.Connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    while (_batches.Count > 0)
                    {
                        var toSend = _batches.Peek();
                        await _service.Connection.InvokeAsync(nameof(IConnectionHubServer.Logs), new ProtocolLogPayload
                        {
                            Logs = toSend
                        });
                        _batches.Dequeue();
                    }
                }
                catch { }
            }
        }

        private LogEvent ChangeLevel(LogEvent s)
        {
            if (s.Level == LogEventLevel.Debug)
                return new LogEvent(s.Timestamp, LogEventLevel.Information, s.Exception, s.MessageTemplate, s.Properties?.Select(r => new LogEventProperty(r.Key, r.Value)));
            return s;
        }

        private string Serialize(LogEvent logEvent)
        {
            var s = new StringWriter();
            _formatter.Format(logEvent, s);
            return s.ToString();
        }

        public Task OnEmptyBatchAsync() => Task.CompletedTask;
    }
}
