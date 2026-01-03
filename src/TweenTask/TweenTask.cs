using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TweenTasks.Internal;

namespace TweenTasks;

public readonly struct TweenTask : IEquatable<TweenTask>
{
    public static TweenBuilderEntry<float, FloatTweenAdapter> Create(float start, float end, double duration)
    {
        return new TweenBuilderEntry<float, FloatTweenAdapter>(new(start, end), duration);
    }

    public static TweenToBuilderEntry<float, FloatTweenAdapter> Create(float end, double duration)
    {
        return new TweenToBuilderEntry<float, FloatTweenAdapter>(new(0, end), duration);
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
        promise.PlaybackSpeed = speed;
    }

    public bool TryCancel()
    {
        return promise.TryCancel(token);
    }

    public bool TryComplete()
    {
        return promise.TryComplete(token);
    }

    public ValueTask AsValueTask()
    {
        return new(promise, token);
    }

    public void Forget()
    {
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