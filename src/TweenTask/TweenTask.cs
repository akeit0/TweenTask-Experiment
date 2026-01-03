using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TweenTasks.Internal;

namespace TweenTasks;

public readonly struct TweenTask : IEquatable<TweenTask>
{
    public static TweenBuilderEntry<float, FloatTweenAdapter> Create(float start, float end, double duration)
    {
        return new(new(start, end), duration);
    }

    public static TweenToBuilderEntry<float, FloatTweenAdapter> Create(float end, double duration)
    {
        return new(new(end), duration);
    }

    private readonly TweenPromise promise;
    private readonly short token;

    internal TweenTask(TweenPromise promise, short token)
    {
        this.promise = promise;
        this.token = token;
    }

    public void SetPlaybackSpeed(double speed)
    {
        Validate();
        promise.PlaybackSpeed = speed;
    }

    public double Time
    {
        get
        {
            Validate();
            return promise.Time;
        }
        set
        {
            Validate();
            promise.SetTime(value);
        }
    }

    public bool TryCancel()
    {
        if (promise == null) return false;
        return promise.TryCancel(token);
    }

    public bool TryComplete()
    {
        if (promise == null) return false;
        return promise.TryComplete(token);
    }

    public ValueTask AsValueTask()
    {
        return new(promise, token);
    }

    public void Forget()
    {
    }

    void Validate()
    {
        if (promise.Version != token)
        {
            throw new InvalidOperationException();
        }
    }

    public ValueTaskAwaiter GetAwaiter()
    {
        return new ValueTask(promise, token).GetAwaiter();
    }

    public bool Equals(TweenTask other)
    {
        return promise == other.promise && token == other.token;
    }

    public override bool Equals(object? obj)
    {
        return obj is TweenTask other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(promise, token);
    }
}