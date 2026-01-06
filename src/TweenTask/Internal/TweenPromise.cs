using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace TweenTasks.Internal;

internal abstract class TweenPromise : IValueTaskSource, IReturnable
{
    protected CancellationToken CancellationToken;
    protected TweenTaskCompletionSourceLightCore Core;
    protected Action<object?, TweenEvent>? EventCallback;
    protected object? EventState;
    protected double Delay;
    protected double Duration;
    protected int LoopCount;
    protected LoopType LoopType;
    protected Ease Ease;
    public double PlaybackSpeed;
    protected object? State;
    public double Time;

    public bool IsPreserved
    {
        get => Core.IsPreserved;
        set => Core.IsPreserved = value;
    }

    public abstract void SetTime(double time);

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

    protected void ReturnWithContinuation(TweenEvent tweenEvent)
    {
        var lastEventCallback = EventCallback;

        if (Core.TrySet())
        {
            var lastState = EventState;

            var hasEvent = Core.HaveEvent;
            if (lastEventCallback == null || (lastEventCallback != LightCallBackWrapper.RunAction && hasEvent))
            {
                TryReturn();
            }
            else
            {
                if (tweenEvent.EventType == TweenEventType.Cancel)
                {
                    var token = CancellationToken;
                    if (token.IsCancellationRequested ||
                        Core.Flags.HasFlags(TweenTaskCompletionLightSourceFlags.ThrowOnManuallyCanceled))
                    {
                        Core.SetCanceledException(token.IsCancellationRequested
                            ? token
                            : CancellationToken.None);
                    }
                }
            }

            if (lastEventCallback == null) return;
            if (hasEvent)
            {
                lastEventCallback!(lastState, tweenEvent);
            }
            else
            {
                Unsafe.As<Action<object>>(lastEventCallback)(lastState!);
            }
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
                ref Unsafe.As<Action<object?, TweenEvent>?, Action<object>?>(ref EventCallback), ref EventState);
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