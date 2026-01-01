using System;
using System.Threading;

namespace TweenTasks
{
    public sealed class TimerTweenRunner : TweenRunner, IDisposable
    {
        static readonly TimerCallback timerCallback = Run;

        readonly object gate = new object();
        bool disposed;
        FreeListCore<ITweenRunnerWorkItem> list;
        ITimer timer;
        double currentTime;
        TimeProvider timeProvider;
        long startTimeStamp;
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
            this.list = new FreeListCore<ITweenRunnerWorkItem>(gate);
            this.timer = timeProvider.CreateStoppedTimer(timerCallback, this);

            // start timer
            this.timer.Change(dueTime, period);
            this.timeProvider = timeProvider;
            startTimeStamp = timeProvider.GetTimestamp();
        }
        
        public override double GetCurrentTime()
        {
            return  TimeSpan.FromTicks(timeProvider.GetTimestamp()-startTimeStamp).TotalSeconds;
        }
        public override void Register(ITweenRunnerWorkItem callback)
        {
            ThrowHelper.ThrowObjectDisposedIf(disposed, typeof(TimerTweenRunner));
            list.Add(callback, out _);
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

        static void Run(object? state)
        {
            var self = (TimerTweenRunner)state!;
            if (self.disposed)
            {
                return;
            }

            lock (self.gate)
            {
                self.currentTime = self.timeProvider.GetElapsedTime(self.startTimeStamp).TotalSeconds;
                var span = self.list.AsSpan();
                for (int i = 0; i < span.Length; i++)
                {
                    ref readonly var item = ref span[i];
                    if (item != null)
                    {
                        try
                        {
                            if (!item.MoveNext(self.currentTime))
                            {
                                self.list.Remove(i);
                            }
                        }
                        catch (Exception ex)
                        {
                            self.list.Remove(i);
                            try
                            {
                                //ObservableSystem.GetUnhandledExceptionHandler().Invoke(ex);
                            }
                            catch { }
                        }
                    }
                }
            }
        }
    }
}

internal static class TimeProviderExtensions
{
    public static ITimer CreateStoppedTimer(this TimeProvider timeProvider, TimerCallback timerCallback, object? state)
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