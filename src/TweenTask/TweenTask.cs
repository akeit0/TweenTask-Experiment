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

    internal readonly TweenPromise Promise;
    private readonly short token;

    internal TweenTask(TweenPromise promise, short token)
    {
        this.Promise = promise;
        this.token = token;
    }

    public void SetPlaybackSpeed(double speed)
    {
        Validate();
        Promise.PlaybackSpeed = speed;
    }
    
    public bool IsPreserved
    {
        get => Promise.IsPreserved;
        set => Promise.IsPreserved = value;
    }

    public double Time
    {
        get
        {
            Validate();
            return Promise.Time;
        }
        set
        {
            Validate();
            Promise.SetTime(value);
        }
    }

    public bool TryCancel()
    {
        if (Promise == null) return false;
        return Promise.TryCancel(token);
    }

    public bool TryComplete()
    {
        if (Promise == null) return false;
        return Promise.TryComplete(token);
    }

    public ValueTask AsValueTask()
    {
        return new(Promise, token);
    }

    public void Forget()
    {
    }

    void Validate()
    {
        if (Promise.Version != token)
        {
            throw new InvalidOperationException();
        }
    }

    public ValueTaskAwaiter GetAwaiter()
    {
        return new ValueTask(Promise, token).GetAwaiter();
    }

    public bool Equals(TweenTask other)
    {
        return Promise == other.Promise && token == other.token;
    }

    public override bool Equals(object? obj)
    {
        return obj is TweenTask other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Promise, token);
    }
}