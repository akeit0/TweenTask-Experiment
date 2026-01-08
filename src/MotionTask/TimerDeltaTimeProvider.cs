using System;
using System.Threading;

namespace MotionTasks;

public sealed class TimerFrameDeltaTimeProvider : FrameDeltaTimeProvider, IDisposable
{
    private static readonly TimerCallback TimerCallback = Run;

    private readonly object gate = new();
    private readonly long startTimeStamp;
    long frameCount;
    private readonly TimeProvider timeProvider;
    private readonly ITimer timer;
    private double currentTime;
    private bool disposed;
    private FreeListCore<IFrameDeltaTimeProviderWorkItem> list;

    public TimerFrameDeltaTimeProvider(TimeSpan period)
        : this(period, period, TimeProvider.System)
    {
    }

    public TimerFrameDeltaTimeProvider(TimeSpan dueTime, TimeSpan period)
        : this(dueTime, period, TimeProvider.System)
    {
    }

    public TimerFrameDeltaTimeProvider(TimeSpan dueTime, TimeSpan period, TimeProvider timeProvider)
    {
        list = new(gate);
        timer = timeProvider.CreateStoppedTimer(TimerCallback, this);

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

    public override long GetFrameCount()
    {
        return frameCount;
    }

    public override void Register(IFrameDeltaTimeProviderWorkItem callback, bool forceNextFrame = true)
    {
        ThrowHelper.ThrowObjectDisposedIf(disposed, typeof(TimerFrameDeltaTimeProvider));
        list.Add(callback, out _);
    }

    private static void Run(object? state)
    {
        var self = (TimerFrameDeltaTimeProvider)state!;
        if (self.disposed) return;

        lock (self.gate)
        {
            var last = self.currentTime;
            self.currentTime = self.timeProvider.GetElapsedTime(self.startTimeStamp).TotalSeconds;
            var delta = self.currentTime - last;
            var info = new FrameInfo(self.frameCount, delta);
            var span = self.list.AsSpan();
            for (var i = 0; i < span.Length; i++)
            {
                ref readonly var item = ref span[i];
                if (item != null)
                    try
                    {
                        if (!item.MoveNext(info)) self.list.Remove(i);
                    }
                    catch (Exception ex)
                    {
                        self.list.Remove(i);
                        try
                        {
                            MotionSystem.GetUnhandledExceptionHandler().Invoke(ex);
                        }
                        catch
                        {
                        }
                    }
            }

            self.frameCount++;
        }
    }
}