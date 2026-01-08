using System;
using System.Diagnostics;
using System.Threading;
using MotionTasks;
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
        CancellationToken cancellationToken, TweenTaskSettingFlags flags, out short token)
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
        promise.Core.Flags = new TweenTaskFlagsWrapper((int)flags);
        promise.Core.Activate();

        if (endCallback != null) promise.Core.HaveEvent = true;
        promise.Time = 0;
        token = promise.Core.Version;
        return promise;
    }

    public static double TotalDuration(double delay, double duration, int loopCount, TweenTaskFlagsWrapper flags)
    {
        if (flags.HasFlags(TweenTaskCompletionLightSourceFlags.DelayEveryLoop))
        {
            return delay * loopCount + duration * loopCount;
        }
        else
        {
            return delay + duration * loopCount;
        }
    }

    public override void SetTime(double time)
    {
        var lastTime = Time;
        Time = time;
        if (CancellationToken.IsCancellationRequested)
        {
            ReturnWithContinuation(new TweenEvent(TweenEventType.Cancel));

            return;
        }

        var delayEveryLoop = Core.Flags.HasFlags(TweenTaskCompletionLightSourceFlags.DelayEveryLoop);

        var position = Time;
        var perLoopDuration = Duration;
        var perLoopDelay = Delay;
        if (!delayEveryLoop)
        {
            perLoopDelay = 0;
            position -= Delay;
        }

        perLoopDuration += perLoopDelay;


        var progress = position / (perLoopDuration);
        var currentLoopCount = (Math.Max(0, (int)progress));

        var lastPosition = lastTime;
        if (!delayEveryLoop)
        {
            lastPosition -= Delay;
        }

        var lastLoopCount = (int)(Math.Max(0, lastPosition) / perLoopDuration);
        var isComplete = false;
        if (currentLoopCount >= LoopCount)
        {
            isComplete = true;
            if (lastLoopCount >= LoopCount)
            {
                return;
            }
        }

        var perLoopProgress = (position - (currentLoopCount * perLoopDuration) - perLoopDelay) / perLoopDuration;
        if (delayEveryLoop)
        {
            perLoopProgress *= ((perLoopDuration) / Duration);
        }

        if (isComplete)
        {
            perLoopProgress = 1;
        }

        if (perLoopProgress < 0)
        {
            if (currentLoopCount != lastLoopCount ||
                Core.Flags.HasFlags(TweenTaskCompletionLightSourceFlags.ApplyValuesDuringDelay))
            {
                perLoopProgress = 0;
            }
            else
            {
                return;
            }
        }


        double easedValue =
            TweenMath.CalculateProgress(perLoopProgress, currentLoopCount - (isComplete ? 1 : 0), LoopType, Ease);

        try
        {
            action?.Invoke(State, adapter.Evaluate(easedValue));
        }
        catch (Exception e)
        {
            MotionSystem.GetUnhandledExceptionHandler()(e);
        }


        var lastEventCallback = EventCallback;
        if (lastEventCallback != null && Core.HaveEvent)
        {
            while (lastLoopCount < currentLoopCount)
            {
                lastLoopCount++;
                lastEventCallback(EventState, new TweenEvent(TweenEventType.LoopComplete, lastLoopCount, LoopCount));
            }
        }

        if (!isComplete) return;


        if (!Core.IsPreserved)
            ReturnWithContinuation(new TweenEvent(TweenEventType.Complete, currentLoopCount, LoopCount));

        return;
    }

    public bool MoveNext(FrameInfo frameInfo)
    {
        if (!Core.IsActive) return false;
        var lastTime = Time;
        var deltaTime = frameInfo.DeltaTime;
        Time += PlaybackSpeed * deltaTime;
        if (CancellationToken.IsCancellationRequested)
        {
            ReturnWithContinuation(new TweenEvent(TweenEventType.Cancel));

            return false;
        }

        var delayEveryLoop = Core.Flags.HasFlags(TweenTaskCompletionLightSourceFlags.DelayEveryLoop);

        var position = Time;
        var perLoopDuration = Duration;
        var perLoopDelay = Delay;
        if (!delayEveryLoop)
        {
            perLoopDelay = 0;
            position -= Delay;
        }

        perLoopDuration += perLoopDelay;


        var progress = position / (perLoopDuration);
        var currentLoopCount = (Math.Max(0, (int)progress));

        var lastPosition = lastTime;
        if (!delayEveryLoop)
        {
            lastPosition -= Delay;
        }

        var lastLoopCount = (int)(Math.Max(0, lastPosition) / perLoopDuration);
        var isComplete = currentLoopCount >= LoopCount;

        var perLoopProgress = (position - (currentLoopCount * perLoopDuration) - perLoopDelay) / perLoopDuration;
        if (delayEveryLoop)
        {
            perLoopProgress *= ((perLoopDuration) / Duration);
        }

        if (isComplete)
        {
            perLoopProgress = 1;
        }

        if (perLoopProgress < 0)
        {
            if (currentLoopCount != lastLoopCount ||
                Core.Flags.HasFlags(TweenTaskCompletionLightSourceFlags.ApplyValuesDuringDelay))
            {
                perLoopProgress = 0;
            }
            else
            {
                return true;
            }
        }

        double easedValue = TweenMath.CalculateProgress(perLoopProgress, currentLoopCount, LoopType, Ease);

        try
        {
            action?.Invoke(State, adapter.Evaluate(easedValue));
        }
        catch (Exception e)
        {
            MotionSystem.GetUnhandledExceptionHandler()(e);
            if (Core.Flags.HasFlags(TweenTaskCompletionLightSourceFlags.CancelOnError))
            {
                ReturnWithContinuation(new TweenEvent(TweenEventType.Cancel));

                return false;
            }
        }

        var lastEventCallback = EventCallback;
        if (lastEventCallback != null && Core.HaveEvent)
        {
            while (lastLoopCount < currentLoopCount)
            {
                lastLoopCount++;
                lastEventCallback(EventState, new TweenEvent(TweenEventType.LoopComplete, lastLoopCount));
            }
        }

        if (currentLoopCount < LoopCount) return true;

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