using System;
using System.Buffers;
using System.Threading;
using TweenTasks.Internal;

namespace TweenTasks;

[MustUseThis("Schedule or Build")]
public readonly struct TweenSequenceBuilder
{
    readonly int version;
    internal readonly TweenSequenceBuilderBuffer buffer;

    internal TweenSequenceBuilder(TweenSequenceBuilderBuffer buffer, int version)
    {
        this.buffer = buffer;
        this.version = version;
    }

    public TweenSequenceBuilder Append<TValue, TAdapter>(TweenBuilder<TValue, TAdapter> builder)
        where TAdapter : ITweenAdapter<TValue>
    {
        Validate();
        buffer.Insert(buffer.Duration, builder.Buffer);
        return this;
    }

    public TweenSequenceBuilder Insert<TValue, TAdapter>(double time, TweenBuilder<TValue, TAdapter> builder)
        where TAdapter : ITweenAdapter<TValue>
    {
        Validate();
        buffer.Insert(time, builder.Buffer);
        return this;
    }

    public TweenSequenceBuilder Join<TValue, TAdapter>(TweenBuilder<TValue, TAdapter> builder)
        where TAdapter : ITweenAdapter<TValue>
    {
        Validate();
        buffer.Insert(buffer.LastStart, builder.Buffer);
        return this;
    }

    public TweenTask Schedule(CancellationToken cancellationToken = default)
    {
        Validate();
        return buffer.Schedule(TweenSystem.DefaultFrameDeltaTimeProvider,cancellationToken);
    }
    
    public TweenTask Schedule(FrameDeltaTimeProvider provider,CancellationToken cancellationToken = default)
    {
        Validate();
        return buffer.Schedule(provider,cancellationToken);
    }

    public void Validate()
    {
        if (buffer.Version != version)
        {
            throw new InvalidOperationException("Tween buffer versions do not match.");
        }
    }
}

internal class TweenSequenceBuilderBuffer : ITaskPoolNode<TweenSequenceBuilderBuffer>, IReturnable
{
    static TaskPool<TweenSequenceBuilderBuffer> pool;
    internal ArrayPool<TweenSequenceItem> ItemArrayPool;
    internal TweenSequenceItem[]? TweensBuffer;
    internal int TweenCount;
    internal object? EndState;
    internal Action<object?, TweenEvent>? OnEndAction;
    internal double LastStart;
    internal double Duration;
    internal int LoopCount = 1;
    internal LoopType LoopType;
    internal Ease Ease;
    internal int Version;

    TweenSequenceBuilderBuffer? next;
    ref TweenSequenceBuilderBuffer? ITaskPoolNode<TweenSequenceBuilderBuffer>.NextNode => ref next;

    public static TweenSequenceBuilderBuffer Create(ArrayPool<TweenSequenceItem> arrayPool, out int version)
    {
        if (!pool.TryPop(out var builder))
        {
            builder = new TweenSequenceBuilderBuffer();
        }

        builder.ItemArrayPool = arrayPool;
        version = builder.Version;
        return builder;
    }

    public void Insert(double position, ITweenBuilderBuffer buffer)
    {
        if (TweensBuffer == null)
        {
            TweensBuffer = ItemArrayPool.Rent(8);
        }

        if (TweensBuffer.Length == TweenCount)
        {
            var newBuffer = ItemArrayPool.Rent(TweenCount * 2);
            TweensBuffer.AsSpan().CopyTo(newBuffer.AsSpan());
            ItemArrayPool.Return(TweensBuffer, true);
            TweensBuffer = newBuffer;
        }

        TweensBuffer[TweenCount] = new TweenSequenceItem(position, buffer);
        TweenCount++;
        LastStart = position;
        Duration = Math.Max(Duration, position + buffer.TotalDuration);
    }

    public TweenTask Schedule(FrameDeltaTimeProvider provider,CancellationToken cancellationToken = default)
    {
        TweensBuffer ??= [];
        Array.Sort(TweensBuffer, 0, TweenCount);
        var promise = SequencePromise.Create(ItemArrayPool, TweensBuffer, TweenCount, 0, Duration, 1, LoopCount,
            LoopType, Ease,
            OnEndAction,
            EndState,
            cancellationToken, out var token);
        provider.Register(promise);
        TryReturn();
        return new TweenTask(promise, token);
    }


    public bool TryReturn()
    {
        TweensBuffer = null;
        OnEndAction = null;
        EndState = null;
        TweenCount = 0;
        Duration = 0;
        LoopCount = 1;
        LoopType = LoopType.Restart;
        Ease = Ease.Linear;
        LastStart = 0;
        Version++;
        pool.TryPush(this);
        return true;
    }
}

public static class TweenSequence
{
    public static TweenSequenceBuilder Create(ArrayPool<TweenSequenceItem>? arrayPool = null)
    {
        if (arrayPool == null)
        {
            arrayPool = ArrayPool<TweenSequenceItem>.Shared;
        }

        return new TweenSequenceBuilder(TweenSequenceBuilderBuffer.Create(arrayPool, out var version), version);
    }
}