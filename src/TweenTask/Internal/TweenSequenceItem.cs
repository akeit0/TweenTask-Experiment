using System;

namespace TweenTasks.Internal;

internal struct TweenSequenceItem : IComparable<TweenSequenceItem>
{
    public TweenSequenceItem(double position, ITweenBuilderBuffer promise)
    {
        Position = position;
        Promise = promise;
    }

    public double Position;
    public ITweenBuilderBuffer Promise;

    public int CompareTo(TweenSequenceItem other)
    {
        return Position.CompareTo(other.Position);
    }
}