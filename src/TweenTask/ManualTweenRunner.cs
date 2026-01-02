using System;

namespace TweenTasks
{
    public sealed class ManualTweenRunner : ITweenRunner, IDisposable
    {
        private readonly object gate = new();
        private double currentTime;
        private bool disposed;
        private FreeListCore<ITweenRunnerWorkItem> list;

        public ManualTweenRunner(double currentTime)
        {
            list = new(gate);

            this.currentTime = currentTime;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                lock (gate)
                {
                    list.Dispose();
                }
            }
        }


        public double GetCurrentTime()
        {
            return currentTime;
        }

        public void Register(ITweenRunnerWorkItem callback)
        {
            ThrowHelper.ThrowObjectDisposedIf(disposed, typeof(TimerTweenRunner));
            list.Add(callback, out _);
        }

        public void Run(double dueTime)
        {
            var self = this;
            if (self.disposed) return;

            lock (self.gate)
            {
                var last = self.currentTime;
                self.currentTime = dueTime;
                var span = self.list.AsSpan();
                for (var i = 0; i < span.Length; i++)
                {
                    ref readonly var item = ref span[i];
                    if (item != null)
                        try
                        {
                            if (!item.MoveNext(self.currentTime - last)) self.list.Remove(i);
                        }
                        catch (Exception ex)
                        {
                            self.list.Remove(i);
                            try
                            {
                                Console.WriteLine(ex);
                                //ObservableSystem.GetUnhandledExceptionHandler().Invoke(ex);
                            }
                            catch
                            {
                            }
                        }
                }
            }
        }
    }
}