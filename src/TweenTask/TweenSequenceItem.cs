using System;

namespace TweenTasks;

public struct TweenSequenceItem : IComparable<TweenSequenceItem>
{
    internal TweenSequenceItem(double position, ITweenBuilderBuffer promise)
    {
        Position = position;
        Promise = promise;
    }

    public readonly double Position;
    internal ITweenBuilderBuffer Promise;

    public int CompareTo(TweenSequenceItem other)
    {
        return Position.CompareTo(other.Position);
    }
}