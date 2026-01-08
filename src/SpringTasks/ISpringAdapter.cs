using System.Numerics;

namespace SpringTasks;

public interface ISpringAdapter<T>
{
    T Evaluate(double deltaTime);
    T Complete();
    public bool IsCompleted { get; }
    T? From => default(T);

    void ApplyFrom(T from, bool isRelative)
    {
    }
}

public interface ISpringAdapter<in TOption, T> : ISpringAdapter<T>
{
    void WithOption(TOption option);
}

public struct Vector2SpringAdapter(Vector2 from, Vector2 to, SpringConfig config)
    : ISpringAdapter<Vector2>
{
    public Vector2 From = from;
    Vector2 ISpringAdapter<Vector2>.From => From;
    public Vector2 To = to;
    private Vector2 _velocity;
    public Vector2 Current = from;

    public Vector2SpringAdapter(Vector2 to, SpringConfig config) : this(default, to, config)
    {
    }

    public Vector2 Evaluate(double deltaTime)
    {
        SpringAnimation.Evaluate(ref Current, ref _velocity, deltaTime, To, config);

        return Current;
    }

    public Vector2 Complete()
    {
        From = To;
        _velocity = Vector2.Zero;
        return From;
    }

    public bool IsCompleted =>
        Vector2.DistanceSquared(Current, To) < config.PositionEpsilon * config.PositionEpsilon
        && (_velocity.LengthSquared()) < config.VelocityEpsilon * config.VelocityEpsilon;

    public void ApplyFrom(Vector2 from, bool isRelative)
    {
        From = from;
        if (isRelative)
        {
            To += from;
        }
    }
}