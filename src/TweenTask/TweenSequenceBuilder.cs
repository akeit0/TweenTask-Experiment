using System;
using System.Collections.Generic;
using System.Threading;
using TweenTasks.Internal;

namespace TweenTasks;
[MustUseThis]
public class TweenSequenceBuilder
{
    internal List<TweenSequenceItem> Tweens = new ();
    internal object? EndState;
    internal Action<object?, TweenResult>? OnEndAction;

    private double total;
    public TweenSequenceBuilder Append<TValue, TAdapter>(TweenBuilder<TValue, TAdapter> builder)where TAdapter : ITweenAdapter<TValue>
    {
        Tweens.Add(new (total,builder.Buffer));
        total+=builder.Buffer.Delay + builder.Buffer.Duration;
        return this;
    }

    public TweenTask Schedule(CancellationToken cancellationToken =default)
    {
        var promise = TweenSequencePromise.Create(Tweens.ToArray(), 0, total, 1, OnEndAction,EndState,cancellationToken, out var token);
        ITweenRunner.Default.Register(promise);
        return new TweenTask(promise,token);
    }
}

public static class TweenSequence
{
    public static TweenSequenceBuilder Create()
    {
        return new TweenSequenceBuilder();
    }
}