using System;

namespace TweenTasks;

public sealed class ManualDeltaTimeProvider : DeltaTimeProvider, IDisposable
{
    private readonly object gate = new();
    private double currentTime;
    private bool disposed;
    private FreeListCore<IDeltaTimeProviderWorkItem> list;

    public ManualDeltaTimeProvider(double currentTime)
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

    public override void Register(IDeltaTimeProviderWorkItem callback)
    {
        ThrowHelper.ThrowObjectDisposedIf(disposed, typeof(TimerDeltaTimeProvider));
        list.Add(callback, out _);
    }

    public void Run(double deltaTime)
    {
        var self = this;
        if (self.disposed) return;

        lock (self.gate)
        {
            var span = self.list.AsSpan();
            for (var i = 0; i < span.Length; i++)
            {
                 var item =  span[i];
                if (item != null)
                    try
                    {
                        if (!item.MoveNext(deltaTime)) self.list.Remove(i);
                    }
                    catch (Exception ex)
                    {
                        self.list.Remove(i);
                        try
                        {
                            TweenSystem.GetUnhandledExceptionHandler().Invoke(ex);
                        }
                        catch
                        {
                        }
                    }
            }
        }
    }
}