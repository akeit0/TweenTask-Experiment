using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace TweenTasks.Internal;

internal abstract class TweenPromise : IValueTaskSource,IReturnable
{
    protected CancellationToken CancellationToken;
    protected TweenTaskCompletionSourceCore Core;
    protected double Delay;
    protected double Duration;
    protected Ease Ease;
    public double PlaybackSpeed;
    protected object? State;
    public double Time;

    public bool IsPreserved
    {
        get
        {
            return Core.IsPreserved;
        }
        set
        {
            Core.IsPreserved = value;
        }
    }

    public virtual void SetTime(double time)
    {
        Time = time;
    }

    public short Version => Core.Version;

    public void GetResult(short token)
    {
        try
        {
            Core.GetResult(token);
        }
        finally
        {
            TryReturn();
        }
    }

    protected void ReturnWithContinuation(TweenResultType result)
    {
        if (Core.TryGetContinuation(out var continuation, out var continuationState))
        {
            TryReturn();
            continuation(continuationState, new(result));
        }
        else
        {
            TryReturn();
        }
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return Core.GetStatus(token);
    }

    public void OnCompleted(Action<object> continuation, object state, short token,
        ValueTaskSourceOnCompletedFlags flags)
    {
        try
        {
            Core.OnCompleted(continuation, state, token);
        }
        catch (Exception e)
        {
            Console.WriteLine(e + new StackTrace().ToString());
        }
    }

    public abstract bool TryComplete(short token);

    public bool TryCancel(short token)
    {
        if (Core.Version != token) return false;
        if (Core.IsSetContinuationWithAwait)
        {
            Core.Deactivate();
            Core.TrySetCanceled(CancellationToken.IsCancellationRequested
                ? CancellationToken
                : CancellationToken.None);
        }
        else
        {
            ReturnWithContinuation(TweenResultType.Cancel);
        }


        return true;
    }


    public abstract bool TryReturn();
}

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

internal class TweenSequencePromise : TweenPromise, ITweenRunnerWorkItem
{
    public TweenSequenceItem[] SequenceItems = [];

    public double LatestTime;

    public static TweenSequencePromise Create(ReadOnlySpan<TweenSequenceItem> items, double delay, double duration,
        double playBackSpeed, Action<object?, TweenResult>? endCallback, object? endState,
        CancellationToken cancellationToken, out short token)
    {
        var promise = new TweenSequencePromise();
        
        promise.SequenceItems = items.ToArray();
        promise.Delay = delay;
        promise.Duration = duration;
        promise.PlaybackSpeed = playBackSpeed;
        promise.CancellationToken = cancellationToken;
        promise.Core.Activate();
        if (endCallback != null) promise.Core.OnCompletedManual(endCallback, endState);
        promise.Time = 0;
        token = promise.Core.Version;
        promise.LatestTime = -1;
        return promise;
    }

    public override bool TryComplete(short token)
    {
        if (Core.Version != token) return false;
        if (Core.IsSetContinuationWithAwait)
            Core.TrySetResult();
        else
            ReturnWithContinuation(TweenResultType.Complete);

        return true;
    }

    public override bool TryReturn()
    {
        Core.Reset();
        foreach (ref var sequenceItem in SequenceItems.AsSpan())
        {
            object p = sequenceItem.Promise;
            if (p is TweenPromise tweenPromise)
            {
                tweenPromise.IsPreserved = false;
            }
            ((IReturnable)p).TryReturn();
        }
        CancellationToken = CancellationToken.None;
        return true;
    }

    public bool MoveNext(double deltaTime)
    {
        if (!Core.IsActive) return false;
        Time += PlaybackSpeed * deltaTime;
        var position = Time - Delay;
        var progress = Math.Min(1, position / Duration);
        if (CancellationToken.IsCancellationRequested)
        {
            if (Core.IsSetContinuationWithAwait)
                Core.TrySetCanceled(CancellationToken);
            else
                ReturnWithContinuation(TweenResultType.Cancel);

            return false;
        }

        if (Delay > Time) return true;

        foreach (ref var sequenceItem in SequenceItems.AsSpan())
        {
            if (sequenceItem.Position > position)
            {
                break;
            }

            if (sequenceItem.Position > LatestTime && sequenceItem.Position < position)
            {
                var t = (sequenceItem.Promise).CreatePromise(out _);
                t.IsPreserved = true;
                sequenceItem.Promise = Unsafe.As<ITweenBuilderBuffer>(t);
            }

            try
            {
                Unsafe.As<TweenPromise>(sequenceItem.Promise).SetTime(position - sequenceItem.Position);
            }
            catch (Exception e)
            {
                if (e is OperationCanceledException operationCanceledException)
                {
                    Console.WriteLine(CancellationToken==operationCanceledException.CancellationToken);
                }
                if (Core.IsSetContinuationWithAwait)
                    Core.TrySetCanceled(CancellationToken);
                else
                    ReturnWithContinuation(TweenResultType.Cancel);
                
                return false;
            }
        }

        LatestTime = Math.Max(LatestTime, position);
        if (progress < 1) return true;

        if (Core.IsSetContinuationWithAwait)
        {
            Core.TrySetResult();
            return false;
        }

        ReturnWithContinuation(TweenResultType.Complete);

        return false;
    }
}

internal class TweenPromise<T, TAdapter> : TweenPromise, ITweenRunnerWorkItem,
    ITaskPoolNode<TweenPromise<T, TAdapter>>
    where TAdapter : ITweenAdapter<T>
{
    private static TaskPool<TweenPromise<T, TAdapter>> pool;
    private Action<object?, T>? action;
    private TAdapter adapter = default!;

    private TweenPromise<T, TAdapter>? next;
    ref TweenPromise<T, TAdapter>? ITaskPoolNode<TweenPromise<T, TAdapter>>.NextNode => ref next;


    public static TweenPromise<T, TAdapter> Create(double delay, double duration, double playBackSpeed, Ease ease,
        TAdapter adapter,
        Action<object?, T>? action, object? state, Action<object?, TweenResult>? endCallback, object? endState,
        CancellationToken cancellationToken, out short token)
    {
        if (!pool.TryPop(out var promise)) promise = new();
        Debug.Assert(!promise.Core.IsSetContinuationWithAwait);
        promise.Delay = delay;
        promise.Duration = duration;
        promise.PlaybackSpeed = playBackSpeed;
        promise.Ease = ease;
        promise.action = action;
        promise.State = state;
        promise.adapter = adapter;
        promise.CancellationToken = cancellationToken;
        promise.Core.Activate();
        
        if (endCallback != null) promise.Core.OnCompletedManual(endCallback, endState);
        promise.Time = 0;
        token = promise.Core.Version;
        return promise;
    }

    public override void SetTime(double time)
    {
        var lastTime = Time;
        Time = time;
        var position = time - Delay;
        var progress = Math.Min(1, position / Duration);
        if (CancellationToken.IsCancellationRequested)
        {
            if (Core.IsSetContinuationWithAwait)
                Core.TrySetCanceled(CancellationToken);
            else
                ReturnWithContinuation(TweenResultType.Cancel);

            return;
        }

        if (Delay > time) return;

        if (progress > 1)
        {
            if (lastTime > time - Delay)
            {
                return;
            }
        }

        action?.Invoke(State, adapter.Evaluate(EaseUtility.Evaluate(progress, Ease)));
        if (progress < 1) return;

        if (Core.IsSetContinuationWithAwait)
        {
            Core.TrySetResult();
            return;
        }


        ReturnWithContinuation(TweenResultType.Complete);

        return;
    }

    public bool MoveNext(double deltaTime)
    {
        if (!Core.IsActive) return false;

        Time += PlaybackSpeed * deltaTime;
        var position = Time - Delay;
        var progress = Math.Min(1, position / Duration);
        if (CancellationToken.IsCancellationRequested)
        {
            if (Core.IsSetContinuationWithAwait)
                Core.TrySetCanceled(CancellationToken);
            else
                ReturnWithContinuation(TweenResultType.Cancel);

            return false;
        }

        if (Delay > Time) return true;

        action?.Invoke(State, adapter.Evaluate(EaseUtility.Evaluate(progress, Ease)));
        if (progress < 1) return true;

        if (Core.IsSetContinuationWithAwait)
        {
            Core.TrySetResult();
            return false;
        }


        ReturnWithContinuation(TweenResultType.Complete);

        return false;
    }


    public override bool TryComplete(short token)
    {
        if (Core.Version != token) return false;
        action?.Invoke(State, adapter.Evaluate(EaseUtility.Evaluate(1, Ease)));
        if (Core.IsSetContinuationWithAwait)
            Core.TrySetResult();
        else
            ReturnWithContinuation(TweenResultType.Complete);

        return true;
    }

    public override bool TryReturn()
    {
        if (Core.IsPreserved) return false;
        Core.Reset();
        CancellationToken = CancellationToken.None;
        action = null!;
        return pool.TryPush(this);
    }
}