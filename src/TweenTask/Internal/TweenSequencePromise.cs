using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TweenTasks.Internal;

internal class SequencePromise : TweenPromise, IDeltaTimeProviderWorkItem, ITaskPoolNode<SequencePromise>
{
    public TweenSequenceItem[] SequenceItems = null!;

    public int SequenceItemCount;
    public int BuiltCount;
    public ArrayPool<TweenSequenceItem> ItemArrayPool;
    static TaskPool<SequencePromise> _pool;

    private SequencePromise? next;
    ref SequencePromise? ITaskPoolNode<SequencePromise>.NextNode => ref next;

    public static SequencePromise Create(ArrayPool<TweenSequenceItem> itemArrayPool,TweenSequenceItem[] items, int count, double delay, double duration,
        double playBackSpeed, int loopCount,
        LoopType loopType, Ease ease, Action<object?, TweenEvent>? endCallback, object? endState,
        CancellationToken cancellationToken, out short token)
    {
        if (!_pool.TryPop(out var promise))
        {
            promise = new SequencePromise();
        }
        promise.ItemArrayPool = itemArrayPool;
        promise.BuiltCount = 0;
        promise.SequenceItems = items;
        promise.SequenceItemCount = count;
        promise.Delay = delay;
        promise.Duration = duration;
        promise.LoopCount = loopCount;
        promise.LoopType = loopType;
        promise.Ease = ease;
        promise.PlaybackSpeed = playBackSpeed;
        promise.CancellationToken = cancellationToken;
        promise.Core.Activate();
        promise.EventCallback = endCallback;
        promise.EventState = endState;
        if (endCallback != null) promise.Core.HaveEvent = true;
        promise.Time = 0;
        token = promise.Core.Version;
        return promise;
    }

    public override bool TryComplete(short token)
    {
        if (Core.Version != token) return false;
        ReturnWithContinuation(new TweenEvent(TweenEventType.Complete));

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

        ItemArrayPool.Return(SequenceItems, true);
        EventCallback = null;
        EventState = null;
        State = null;
        CancellationToken = CancellationToken.None;
        return true;
    }

    public bool MoveNext(double deltaTime)
    {
        if (!Core.IsActive) return false;
        Time += PlaybackSpeed * deltaTime;
        Time = Math.Clamp(Time, 0, Delay + Duration);
        var position = Time - Delay;
        var progress = position / Duration;
        if (CancellationToken.IsCancellationRequested)
        {
            ReturnWithContinuation(new TweenEvent(TweenEventType.Cancel));

            return false;
        }

        if (PlaybackSpeed > 0 && Delay > Time) return true;
        double easedValue = TweenMath.CalculateProgress(progress, LoopCount, LoopType, Ease);

        position = easedValue * Duration;

        var span = SequenceItems.AsSpan(0, SequenceItemCount);
        for (var index = 0; index < span.Length; index++)
        {
            ref var sequenceItem = ref span[index];
            if (PlaybackSpeed > 0 && sequenceItem.Position > position)
            {
                break;
            }

            if (sequenceItem.Position <= position)
            {
                if (BuiltCount <= index)
                {
                    var t = (sequenceItem.Promise).CreatePromise(out _);
                    t.IsPreserved = true;
                    sequenceItem.Promise = Unsafe.As<ITweenBuilderBuffer>(t);
                    BuiltCount++;
                }
            }

            if (BuiltCount <= index) continue;
            try
            {
#if DEBUG
                ((TweenPromise)((object)sequenceItem.Promise)).SetTime(position - sequenceItem.Position);

#else
                Unsafe.As<TweenPromise>(sequenceItem.Promise).SetTime(easedValue - sequenceItem.Position);
#endif
            }
            catch (Exception e)
            {
                TweenSystem.GetUnhandledExceptionHandler()(e);
                ReturnWithContinuation(new TweenEvent(TweenEventType.Cancel));

                return false;
            }
        }

        if (IsPreserved) return true;
        if (progress < 1) return true;
        if (Core.IsPreserved) return true;
        ReturnWithContinuation(new TweenEvent(TweenEventType.Complete));

        return false;
    }
}