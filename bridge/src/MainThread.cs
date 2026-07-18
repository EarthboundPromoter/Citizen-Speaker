using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CSAccessBridge
{
    /// <summary>Marshals bridge-thread work onto Unity's main thread.</summary>
    internal static class MainThread
    {
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

        public static void Drain()
        {
            while (Queue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { Plugin.Log.LogError(e); }
            }
        }

        public static T Run<T>(Func<T> fn, int timeoutMs = 15000)
        {
            using (var done = new ManualResetEventSlim(false))
            {
                T result = default;
                Exception error = null;
                Queue.Enqueue(() =>
                {
                    try { result = fn(); }
                    catch (Exception e) { error = e; }
                    finally { done.Set(); }
                });
                if (!done.Wait(timeoutMs))
                    throw new TimeoutException("Main thread did not service the request (game paused or loading?)");
                if (error != null)
                    throw new InvalidOperationException(error.ToString());
                return result;
            }
        }
    }
}
