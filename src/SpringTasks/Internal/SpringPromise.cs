using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks.Sources;
using MotionTasks;
namespace SpringTasks.Internal;

internal abstract class SpringPromise : IValueTaskSource, IReturnable
{
    protected CancellationToken CancellationToken;
    protected MotionTaskCompletionSourceLightCore Core;
    protected Action<object?, SpringEvent>? EventCallback;
    protected object? EventState;
    public double PlaybackSpeed;
    public LoopType LoopType;
    protected object? State;

    public bool IsPreserved
    {
        get => Core.IsPreserved;
        set => Core.IsPreserved = value;
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

    protected void ReturnWithContinuation(SpringEvent MotionEvent)
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
                if (MotionEvent.EventType == SpringEventType.Cancel)
                {
                    var token = CancellationToken;
                    if (token.IsCancellationRequested ||
                        Core.Flags.HasFlags(SpringTaskCompletionLightSourceFlags.ThrowOnManuallyCanceled))
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
                lastEventCallback!(lastState, MotionEvent);
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
                ref Unsafe.As<Action<object?, SpringEvent>?, Action<object>?>(ref EventCallback), ref EventState);
        }
        catch (Exception e)
        {
            MotionSystem.GetUnhandledExceptionHandler()(e);
        }
    }

    public abstract bool TryComplete(short token);

    public bool TryCancel(short token)
    {
        if (Core.Version != token) return false;
        Core.Deactivate();
        ReturnWithContinuation(new SpringEvent(SpringEventType.Cancel));
        return true;
    }


    public abstract bool TryReturn();
}