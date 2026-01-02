using System;
using System.Threading;

namespace TweenTasks
{
    public sealed class TimerTweenRunner : ITweenRunner, IDisposable
    {
        private static readonly TimerCallback timerCallback = Run;

        private readonly object gate = new();
        private readonly long startTimeStamp;
        private readonly TimeProvider timeProvider;
        private readonly ITimer timer;
        private double currentTime;
        private bool disposed;
        private FreeListCore<ITweenRunnerWorkItem> list;

        public TimerTweenRunner(TimeSpan period)
            : this(period, period, TimeProvider.System)
        {
        }

        public TimerTweenRunner(TimeSpan dueTime, TimeSpan period)
            : this(dueTime, period, TimeProvider.System)
        {
        }

        public TimerTweenRunner(TimeSpan dueTime, TimeSpan period, TimeProvider timeProvider)
        {
            list = new(gate);
            timer = timeProvider.CreateStoppedTimer(timerCallback, this);

            // start timer
            timer.Change(dueTime, period);
            this.timeProvider = timeProvider;
            startTimeStamp = timeProvider.GetTimestamp();
            currentTime = 0;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                lock (gate)
                {
                    timer.Dispose();
                    list.Dispose();
                }
            }
        }

        public double GetCurrentTime()
        {
            return TimeSpan.FromTicks(timeProvider.GetTimestamp() - startTimeStamp).TotalSeconds;
        }

        public void Register(ITweenRunnerWorkItem callback)
        {
            ThrowHelper.ThrowObjectDisposedIf(disposed, typeof(TimerTweenRunner));
            list.Add(callback, out _);
        }

        private static void Run(object? state)
        {
            var self = (TimerTweenRunner)state!;
            if (self.disposed) return;

            lock (self.gate)
            {
                var last = self.currentTime;
                self.currentTime = self.timeProvider.GetElapsedTime(self.startTimeStamp).TotalSeconds;
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

    internal static class TimeProviderExtensions
    {
        public static ITimer CreateStoppedTimer(this TimeProvider timeProvider, TimerCallback timerCallback,
            object? state)
        {
            return timeProvider.CreateTimer(timerCallback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public static void RestartImmediately(this ITimer timer)
        {
            timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        public static void InvokeOnce(this ITimer timer, TimeSpan dueTime)
        {
            timer.Change(dueTime, Timeout.InfiniteTimeSpan);
        }

        public static void Stop(this ITimer timer)
        {
            timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }
}