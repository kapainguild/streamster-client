using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Streamster.ClientData
{
    public class TaskHelper
    {
        public static async Task GoToPool()
        {
            await Task.Yield();
        }

        public static void RunUnawaited(Func<Task> taskGetter, string name)
        {
            _ = RunUnwaitedAsync(taskGetter(), name);
        }

        public static void RunUnawaited(Task task, string name)
        {
            _ = RunUnwaitedAsync(task, name);
        }

        public static Task RunSafelyAsync(Func<Task> taskGetter, string name) => RunSafelyAsync(taskGetter(), name);

        public static async Task RunSafelyAsync(Task task, string name)
        {
            try
            {
                await task;
            }
            catch (Exception e) when (IsCancellation(e)) { }
            catch (Exception e)
            {
                Log.Error(e, $"Safe call '{name}' failed");
            }
        }

        public static Task WaitOneAsync(WaitHandle waitHandle, string message)
        {
            var tcs = new TaskCompletionSource<bool>();
            var rwh = ThreadPool.RegisterWaitForSingleObject(waitHandle,
                delegate 
                {
                    Log.Information(message);
                    tcs.TrySetResult(true); 
                }, null, -1, true);
            var t = tcs.Task;
            t.ContinueWith((antecedent) => rwh.Unregister(null));
            return t;
        }

        public static bool IsCancellation(Exception e) => e is TaskCanceledException || e is OperationCanceledException;

        private static async Task RunUnwaitedAsync(Task task, string name)
        {
            try
            {
                await task;
            }
            catch (Exception e) when (IsCancellation(e)) { }
            catch (Exception e)
            {
                Log.Error(e, $"Unawaited '{name}' failed");
            }
        }
    }

    public  class TaskEntry
    {
        public Task Task { get; set; }

        public CancellationTokenSource CancellationTokenSource { get; set; }

        public TaskEntry(Task task, CancellationTokenSource cancellationTokenSource)
        {
            Task = task;
            CancellationTokenSource = cancellationTokenSource;
        }
    }
}
