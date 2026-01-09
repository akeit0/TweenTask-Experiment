using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SpringTasks.Internal;

namespace SpringTasks;

public readonly struct SpringTask : IEquatable<SpringTask>
{
    public static SpringBuilderEntry<float, FloatSpringAdapter> Create(float from, float to, SpringConfig config)
    {
        return new SpringBuilderEntry<float, FloatSpringAdapter>(new FloatSpringAdapter(from, to, config));
    }
    internal readonly SpringPromise Promise;
    private readonly short token;

    internal SpringTask(SpringPromise promise, short token)
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

    public bool Equals(SpringTask other)
    {
        return Promise == other.Promise && token == other.token;
    }

    public override bool Equals(object? obj)
    {
        return obj is SpringTask other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Promise, token);
    }
}