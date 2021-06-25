using Serilog;
using Serilog.Core;
using Serilog.Events;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Logging
{
    public class LogService
    {
        private LoggingLevelSwitch _switch = new LoggingLevelSwitch(LogEventLevel.Information);

        public LogService(IAppEnvironment environment, Func<HubConnectionService> hubConnectionServiceFactory)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_switch)
#if DEBUG
                //.WriteTo.Async(a =>
                //{
                //    a.Debug(outputTemplate: "[{Timestamp:dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");
                //})
#endif
                .WriteTo.File(path: $"{environment.GetStorageFolder()}\\Logs\\Client.txt",
                                fileSizeLimitBytes: 10_000_000,
                                retainedFileCountLimit: 2,
                                rollOnFileSizeLimit: true,
                //                flushToDiskInterval: TimeSpan.Zero,
                                outputTemplate: "[{Timestamp:dd HH:mm:ss.fff}] {Level:u3}]: {Message:lj}{NewLine}{Exception}")
                .WriteTo.ServerLog(hubConnectionServiceFactory)
                .CreateLogger();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Log.Information($"Application started '{ClientVersionHelper.GetVersion()}'");
        }

        public void EnableDebug()
        {
            _switch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ee)
                Log.Fatal(ee, "ClientCurrentDomainUnhandledException");
            else
                Log.Fatal($"ClientCurrentDomainUnhandledException ({e.ExceptionObject})");

            if (e.IsTerminating)
            {
                Task.Run(() => Log.CloseAndFlush());
                Thread.Sleep(4000);
            }
        }

        
    }
}
