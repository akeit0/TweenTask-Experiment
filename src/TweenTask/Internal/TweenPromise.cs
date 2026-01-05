using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace TweenTasks.Internal;

internal abstract class TweenPromise : IValueTaskSource, IReturnable
{
    protected CancellationToken CancellationToken;
    protected TweenTaskCompletionSourceLightCore Core;
    protected double Delay;
    protected double Duration;
    protected int LoopCount;
    protected LoopType LoopType;
    protected Ease Ease;
    public double PlaybackSpeed;
    protected object? State;
    public double Time;

    protected Action<object?, TweenEvent>? EventCallback;
    protected object? EventState;

    public bool IsPreserved
    {
        get => Core.IsPreserved;
        set => Core.IsPreserved = value;
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

    protected void ReturnWithContinuation(TweenEvent @event)
    {
        var lastEventCallback = EventCallback;

        if (Core.TrySet())
        {
            var lastState = EventState;

            if (lastEventCallback == null || (lastEventCallback != LightCallBackWrapper.RunAction && Core.HaveEvent))
            {
                TryReturn();
            }
            else
            {
                if (@event.EventType == TweenEventType.Cancel)
                {
                    Core.SetCanceledException(CancellationToken.IsCancellationRequested ? CancellationToken : default);
                }
            }

            if (lastEventCallback == null) return;
            Core.RunContinuation(lastEventCallback, lastState, @event);
        }
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return Core.GetStatus(EventCallback, token);
    }

    public void OnCompleted(Action<object> continuation, object state, short token,
        ValueTaskSourceOnCompletedFlags flags)
    {
        try
        {
            Core.OnCompleted(continuation, state, token,
                ref Unsafe.As<Action<object, TweenEvent>, Action<object>>(ref EventCallback), ref EventState);
        }
        catch (Exception e)
        {
            TweenSystem.GetUnhandledExceptionHandler()(e);
        }
    }

    public abstract bool TryComplete(short token);

    public bool TryCancel(short token)
    {
        if (Core.Version != token) return false;
        Core.Deactivate();
        ReturnWithContinuation(new TweenEvent(TweenEventType.Cancel));


        return true;
    }


    public abstract bool TryReturn();
}

internal static class TweenMath
{
    public static double CalculateProgress(double progress, int loopCount,
        LoopType loopType, Ease ease)
    {
        var offset = 0.0;
        var factor = 1.0;
        if (loopCount > 1 && progress >= 1)
        {
            var currentLoop = (int)(progress);
            var loopProgress = progress - currentLoop;
            if (currentLoop % 2 == 1 && loopType is LoopType.Yoyo or LoopType.Flip)
            {
                if (loopType == LoopType.Flip)
                {
                    offset = 1;
                    factor = -1;
                    progress = loopProgress;
                }
                else
                {
                    progress = 1 - loopProgress;
                }
            }
            else
            {
                progress = loopProgress;
                if (loopType == LoopType.Incremental)
                {
                    offset = currentLoop * 1; //EaseUtility.Evaluate(1, ease);
                }
            }
        }

        return offset + factor * EaseUtility.Evaluate(Math.Clamp(progress, 0, 1), ease);
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

        action?.Invoke(State, adapter.Evaluate(easedValue));
        if (totalProgress < 1) return;

        if (!Core.IsPreserved)
            ReturnWithContinuation(new TweenEvent(TweenEventType.Complete));

        return;
    }

    public bool MoveNext(double deltaTime)
    {
        if (!Core.IsActive) return false;

        Time += PlaybackSpeed * deltaTime;
        var position = Time - Delay;
        var progress = position / Duration;
        if (CancellationToken.IsCancellationRequested)
        {
            ReturnWithContinuation(new TweenEvent(TweenEventType.Cancel));

            return false;
        }

        if (Delay > Time) return true;
        var totalProgress = progress / LoopCount;
        double easedValue = TweenMath.CalculateProgress(progress, LoopCount, LoopType, Ease);

        //Console.WriteLine(adapter+" " +adapter.Evaluate(easedValue));
        action?.Invoke(State, adapter.Evaluate(easedValue));
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