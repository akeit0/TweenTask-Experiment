
using System;
using System.Diagnostics;
using System.Threading;

namespace TweenTasks.Internal;


internal class TweenPromise<T, TAdapter> : TweenPromise, IFrameDeltaTimeProviderWorkItem,
    ITaskPoolNode<TweenPromise<T, TAdapter>>
    where TAdapter : ITweenAdapter<T>
{
    private static TaskPool<TweenPromise<T, TAdapter>> pool;
    private Action<object?, T>? action;
    private TAdapter adapter = default!;

    private TweenPromise<T, TAdapter>? next;
    ref TweenPromise<T, TAdapter>? ITaskPoolNode<TweenPromise<T, TAdapter>>.NextNode => ref next;


    public static TweenPromise<T, TAdapter> Create(double delay, double duration, double playBackSpeed, int loopCount,
        LoopType loopType, Ease ease,
        TAdapter adapter,
        Action<object?, T>? action, object? state, Action<object?, TweenEvent>? endCallback, object? endState,
        CancellationToken cancellationToken, out short token)
    {
        if (!pool.TryPop(out var promise))
        {
            promise = new();
        }

        promise.Delay = delay;
        promise.Duration = duration;
        promise.LoopCount = loopCount;
        promise.LoopType = loopType;
        promise.PlaybackSpeed = playBackSpeed;
        promise.Ease = ease;
        promise.action = action;
        promise.State = state;
        promise.EventCallback = endCallback;
        promise.EventState = endState;
        promise.adapter = adapter;
        promise.CancellationToken = cancellationToken;
        promise.Core.Activate();

        if (endCallback != null) promise.Core.HaveEvent = true;
        promise.Time = 0;
        token = promise.Core.Version;
        return promise;
    }

    public override void SetTime(double time)
    {
        var lastTime = Time;
        Time = time;
        var position = time - Delay;
        var progress = position / Duration;
        if (CancellationToken.IsCancellationRequested)
        {
            ReturnWithContinuation(new TweenEvent(TweenEventType.Cancel));

            return;
        }

        if (Delay > time)
        {
            if (Delay > lastTime)
            {
                return;
            }
        }

        var totalProgress = progress / LoopCount;

        if (totalProgress > 1)
        {
            if ((lastTime - Delay) / LoopCount > Duration)
            {
                return;
            }
        }

        double easedValue = TweenMath.CalculateProgress(progress, LoopCount, LoopType, Ease);

        try
        {
            action?.Invoke(State, adapter.Evaluate(easedValue));
        }
        catch (Exception e)
        {
            TweenSystem.GetUnhandledExceptionHandler()(e);
        }


        var lastEventCallback = EventCallback;
        if (lastEventCallback != null && Core.HaveEvent)
        {
            var lastLoopCount = (int)((lastTime - Delay) / Duration);
            var currentLoopCount = (int)(progress);

            while (lastLoopCount < currentLoopCount)
            {
                lastLoopCount++;
                lastEventCallback(EventState, new TweenEvent(TweenEventType.LoopComplete, lastLoopCount));
            }
        }

        if (totalProgress < 1)
            return;


        if (!Core.IsPreserved)
            ReturnWithContinuation(new TweenEvent(TweenEventType.Complete));

        return;
    }

    public bool MoveNext(FrameInfo frameInfo)
    {
        if (!Core.IsActive) return false;
        var lastTime = Time;
        var deltaTime = frameInfo.DeltaTime;
        Time += PlaybackSpeed * deltaTime;
        var position = Time - Delay;
        var progress = position / Duration;
        if (CancellationToken.IsCancellationRequested)
        {
            ReturnWithContinuation(new TweenEvent(TweenEventType.Cancel));

            return false;
        }

        if (Delay > Time)
        {
            if (Core.Flags.HasFlags(TweenTaskCompletionLightSourceFlags.ApplyValuesDuringDelay))
            {
                progress = 0;
            }
            else
            {
                return true;
            }
        }

        var totalProgress = progress / LoopCount;
        double easedValue = TweenMath.CalculateProgress(progress, LoopCount, LoopType, Ease);

        try
        {
            action?.Invoke(State, adapter.Evaluate(easedValue));
        }
        catch (Exception e)
        {
            TweenSystem.GetUnhandledExceptionHandler()(e);
            if (Core.Flags.HasFlags(TweenTaskCompletionLightSourceFlags.CancelOnError))
            {
                ReturnWithContinuation(new TweenEvent(TweenEventType.Cancel));

                return false;
            }
        }

        var lastEventCallback = EventCallback;
        if (lastEventCallback != null && Core.HaveEvent)
        {
            var lastLoopCount = (int)((lastTime - Delay) / Duration);
            var currentLoopCount = (int)(progress);

            while (lastLoopCount < currentLoopCount)
            {
                lastLoopCount++;
                lastEventCallback(EventState, new TweenEvent(TweenEventType.LoopComplete, lastLoopCount));
            }
        }

        if (totalProgress < 1) return true;

        ReturnWithContinuation(new TweenEvent(TweenEventType.Complete));

        return false;
    }


    public override bool TryComplete(short token)
    {
        if (Core.Version != token) return false;
        action?.Invoke(State, adapter.Evaluate(EaseUtility.Evaluate(1, Ease)));
        ReturnWithContinuation(new TweenEvent(TweenEventType.Complete));

        return true;
    }

    public override bool TryReturn()
    {
        if (Core.IsPreserved) return false;
        Debug.Assert(next == null);
        Core.Reset();
        EventCallback = null;
        EventState = null;
        adapter = default!;
        action = null;
        State = null;
        CancellationToken = CancellationToken.None;
        return pool.TryPush(this);
    }
}