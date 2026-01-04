using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TweenTasks.Internal;

internal class TweenSequencePromise : TweenPromise, ITweenRunnerWorkItem, ITaskPoolNode<TweenSequencePromise>
{
    public TweenSequenceItem[] SequenceItems = null!;

    public int SequenceItemCount;
    public double LatestTime;

    static TaskPool<TweenSequencePromise> _pool;

    private TweenSequencePromise? next;
    ref TweenSequencePromise? ITaskPoolNode<TweenSequencePromise>.NextNode => ref next;

    public static TweenSequencePromise Create(TweenSequenceItem[] items, int count, double delay, double duration,
        double playBackSpeed, Action<object?, TweenResult>? endCallback, object? endState,
        CancellationToken cancellationToken, out short token)
    {
        if (!_pool.TryPop(out var promise))
        {
            promise = new TweenSequencePromise();
        }

        promise.SequenceItems = items;
        promise.SequenceItemCount = count;
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
        if (IsPreserved) return false;
        Core.Reset();
        foreach (ref var sequenceItem in SequenceItems.AsSpan(0, SequenceItemCount))
        {
            object p = sequenceItem.Promise;
            if (p is TweenPromise tweenPromise)
            {
                tweenPromise.IsPreserved = false;
                tweenPromise.SetTime(Duration + 0.001 - sequenceItem.Position);
            }
            else ((IReturnable)p).TryReturn();
        }

        ArrayPool<TweenSequenceItem>.Shared.Return(SequenceItems, true);

        CancellationToken = CancellationToken.None;
        return true;
    }

    public bool MoveNext(double deltaTime)
    {
        if (!Core.IsActive) return false;
        Time += PlaybackSpeed * deltaTime;
        Time = Math.Clamp(Time, 0, Delay + Duration);
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

        if (PlaybackSpeed > 0 && Delay > Time) return true;

        foreach (ref var sequenceItem in SequenceItems.AsSpan(0, SequenceItemCount))
        {
            if (PlaybackSpeed > 0 && sequenceItem.Position > position)
            {
                break;
            }

            if (sequenceItem.Position > LatestTime)
            {
                var t = (sequenceItem.Promise).CreatePromise(out _);
                t.IsPreserved = true;
                sequenceItem.Promise = Unsafe.As<ITweenBuilderBuffer>(t);
            }

            try
            {
#if DEBUG
                ((TweenPromise)((object)sequenceItem.Promise)).SetTime(position - sequenceItem.Position);

#else
                Unsafe.As<TweenPromise>(sequenceItem.Promise).SetTime(position - sequenceItem.Position);
#endif
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                if (Core.IsSetContinuationWithAwait)
                    Core.TrySetCanceled(CancellationToken);
                else
                    ReturnWithContinuation(TweenResultType.Cancel);

                return false;
            }
        }

        LatestTime = Math.Max(LatestTime, position);
        if (IsPreserved) return true;
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