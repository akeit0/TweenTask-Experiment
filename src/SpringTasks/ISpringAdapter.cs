using System;
using System.Numerics;

namespace SpringTasks;

public delegate void AdapterModifier<T, in TState>(TState state,ref T adapter) where TState : class?;
public interface ISpringAdapter<T>
{
    T Evaluate(double deltaTime);
    T Complete();
    public bool IsCompleted { get; }
    T? From => default(T);

    void ApplyFrom(T from, bool isRelative)
    {
    }

    void ApplyTo(T to);

}

public interface ISpringAdapter<in TOption, T> : ISpringAdapter<T>
{
    void WithOption(TOption option);
}

public struct Vector2SpringAdapter(Vector2 from, Vector2 to, SpringConfig config)
    : ISpringFromAdapter<Vector2>
{
    public Vector2 From = from;
    Vector2 ISpringAdapter<Vector2>.From => From;
    public Vector2 To = to;
    public Vector2 Velocity;
    public Vector2 Current = from;
    public SpringConfig Config = config;

    public Vector2SpringAdapter(Vector2 to, SpringConfig config) : this(default, to, config)
    {
    }

    public Vector2 Evaluate(double deltaTime)
    {
        SpringAnimation.Evaluate(ref Current, ref Velocity, (float)deltaTime, To, Config.Frequency, Config.DumpingRatio);

        return Current;
    }

    public Vector2 Complete()
    {
        From = To;
        Velocity = Vector2.Zero;
        return From;
    }

    public bool IsCompleted =>
        Vector2.DistanceSquared(Current, To) <Config.PositionEpsilon * MathF.Abs(Config.PositionEpsilon)
        && (Velocity.LengthSquared()) < Config.VelocityEpsilon * Config.VelocityEpsilon;

    public void ApplyFrom(Vector2 from, bool isRelative)
    {
        From = from;
        Current = from;
        if (isRelative)
        {
            To += from;
        }
    }
    public void ApplyTo(Vector2 to)
    {
        To = to;
    }
}

public struct FloatSpringAdapter(float from, float to, SpringConfig config)
    : ISpringFromAdapter<float>
{
    public float From = from;
    float ISpringAdapter<float>.From => From;
    public float To = to;
    public float Velocity;
    public float Current = from;
    public SpringConfig Config = config;

    public FloatSpringAdapter(float to, SpringConfig config) : this(default, to, config)
    {
    }

    public float Evaluate(double deltaTime)
    {
        SpringAnimation.Evaluate(ref Current, ref Velocity, (float)deltaTime, To, Config.Frequency, Config.DumpingRatio);

        return Current;
    }

    public float Complete()
    {
        From = To;
        Velocity = 0;
        return From;
    }

    public bool IsCompleted =>
        MathF.Abs(Current- To) <Config.PositionEpsilon 
        && (MathF.Abs(Velocity)) <  Config.VelocityEpsilon;

    public void ApplyFrom(float from, bool isRelative)
    {
        From = from;
        Current = from;
        if (isRelative)
        {
            To += from;
        }
    }
    public void ApplyTo(float to)
    {
        To = to;
    }
}