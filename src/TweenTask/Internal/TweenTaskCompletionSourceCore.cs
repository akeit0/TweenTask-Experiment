using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace TweenTasks.Internal;

[Flags]
internal enum TweenTaskCompletionLightSourceFlags
{
    None = 0,
    Preserved = 1,
    ApplyValuesDuringDelay = Preserved << 1,
    DelayEveryLoop = ApplyValuesDuringDelay << 1,
    CancelOnError = DelayEveryLoop << 1,
    ThrowOnManuallyCanceled = CancelOnError << 1,
    ThrowBindActionException = ThrowOnManuallyCanceled << 1,
    HasEvent = ThrowBindActionException << 1,
    Pooled = HasEvent << 1,
    Done = Pooled << 1,
    HasHandledError = Done << 1,
}

internal struct TweenTaskFlags(int flags)
{
    public int Flags = flags;

    public bool TrySetFlags(TweenTaskCompletionLightSourceFlags setFlags)
    {
        int oldFlags, newFlags;
        do
        {
            oldFlags = Flags;
            newFlags = oldFlags | (int)setFlags;
            if (oldFlags == newFlags)
                return false;
        } while (Interlocked.CompareExchange(ref Flags, newFlags, oldFlags) != oldFlags);

        return true;
    }

    public bool HasFlags(TweenTaskCompletionLightSourceFlags checkFlags)
    {
        return (Flags & (int)checkFlags) != 0;
    }

    public void SetFlags(TweenTaskCompletionLightSourceFlags setFlags)
    {
        Flags |= (int)setFlags;
    }

    public void ClearFlags(TweenTaskCompletionLightSourceFlags clearFlags)
    {
        Flags &= ~(int)clearFlags;
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct TweenTaskCompletionSourceLightCore
{
    private object? error; // ExceptionHolder or OperationCanceledException
    internal TweenTaskFlags Flags;

    /// <summary>Gets the operation version.</summary>
    [DebuggerHidden]
    public short Version { get; private set; }

    public void Activate()
    {
        Flags.SetFlags(TweenTaskCompletionLightSourceFlags.Pooled);
    }

    public void Deactivate()
    {
        Flags.SetFlags(TweenTaskCompletionLightSourceFlags.Pooled);
    }

    public bool IsActive => Flags.HasFlags(TweenTaskCompletionLightSourceFlags.Pooled);

    public bool IsPreserved
    {
        get => Flags.HasFlags(TweenTaskCompletionLightSourceFlags.Preserved);
        set
        {
            if (value)
            {
                Flags.SetFlags(TweenTaskCompletionLightSourceFlags.Preserved);
            }
            else
            {
                Flags.ClearFlags(TweenTaskCompletionLightSourceFlags.Preserved);
            }
        }
    }

    internal void MarkHandled()
    {
        Flags.ClearFlags(TweenTaskCompletionLightSourceFlags.HasHandledError);
    }

    [DebuggerHidden]
    public void Reset()
    {
        ReportUnhandledError();

        unchecked
        {
            Version += 1; // incr version.
        }

        error = null;
        Flags = new TweenTaskFlags((int)TweenTaskCompletionLightSourceFlags.Pooled);
    }

    private void ReportUnhandledError()
    {
        if (Flags.HasFlags(TweenTaskCompletionLightSourceFlags.HasHandledError))
            try
            {
                if (error is OperationCanceledException oc)
                {
                    TweenSystem.GetUnhandledExceptionHandler().Invoke(oc);
                }
                else if (error is ExceptionHolder e)
                {
                    TweenSystem.GetUnhandledExceptionHandler().Invoke(e.GetException().SourceException);
                }
            }
            catch
            {
            }
    }

    public bool HaveEvent
    {
        get => Flags.HasFlags(TweenTaskCompletionLightSourceFlags.HasEvent);
        set
        {
            if (value)
            {
                Flags.SetFlags(TweenTaskCompletionLightSourceFlags.HasEvent);
            }
            else
            {
                Flags.ClearFlags(TweenTaskCompletionLightSourceFlags.HasEvent);
            }
        }
    }

    /// <summary>Completes with a successful result.</summary>
    [DebuggerHidden]
    public bool TrySet()
    {
        return Flags.TrySetFlags(TweenTaskCompletionLightSourceFlags.Done);
    }

    public void SetCanceledException(CancellationToken cancellationToken)
    {
        Flags.SetFlags(TweenTaskCompletionLightSourceFlags.HasHandledError);
        error = cancellationToken == CancellationToken.None
            ? defaultCancelledException
            : new OperationCanceledException(cancellationToken);
    }

    public void RunContinuation(Action<object?, TweenEvent>? continuation, object continuationState,
        TweenEvent tweenEvent)
    {
        if (HaveEvent)
        {
            continuation!(continuationState, tweenEvent);
        }
        else
        {
            Unsafe.As<Action<object>>(continuation!)(continuationState);
        }
    }


    private static readonly OperationCanceledException defaultCancelledException = new(CancellationToken.None);


    /// <summary>Gets the status of the operation.</summary>
    /// <param name="token">Opaque value that was provided to the <see cref="TweenTask" />'s constructor.</param>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTaskSourceStatus GetStatus(object? continuation, short token)
    {
        ValidateToken(token);
        return continuation == null || (!Flags.HasFlags(TweenTaskCompletionLightSourceFlags.Done))
            ? ValueTaskSourceStatus.Pending
            : error == null
                ? ValueTaskSourceStatus.Succeeded
                : error is OperationCanceledException
                    ? ValueTaskSourceStatus.Canceled
                    : ValueTaskSourceStatus.Faulted;
    }

    /// <summary>Gets the result of the operation.</summary>
    /// <param name="token">Opaque value that was provided to the <see cref="TweenTask" />'s constructor.</param>
    // [StackTraceHidden]
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetResult(short token)
    {
        ValidateToken(token);

        if (!Flags.HasFlags(TweenTaskCompletionLightSourceFlags.Done))
            throw new InvalidOperationException("Not yet completed, TweenTask only allow to use await.");

        if (error != null)
        {
            Flags.ClearFlags(TweenTaskCompletionLightSourceFlags.HasHandledError);
            if (error is OperationCanceledException oce)
            {
                if (oce == defaultCancelledException)
                {
                    throw new OperationCanceledException();
                }

                throw oce;
            }

            if (error is ExceptionHolder eh) eh.GetException().Throw();

            throw new InvalidOperationException("Critical: invalid exception type was held.");
        }
    }

    /// <summary>Schedules the continuation action for this operation.</summary>
    /// <param name="continuation">The continuation to invoke when the operation has completed.</param>
    /// <param name="state">The state object to pass to <paramref name="continuation" /> when it's invoked.</param>
    /// <param name="token">Opaque value that was provided to the <see cref="TweenTask" />'s constructor.</param>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnCompleted(Action<object> continuation, object state,
        short token /*, ValueTaskSourceOnCompletedFlags flags */, ref Action<object>? contRef, ref object? contState)
    {
        if (continuation == null) throw new ArgumentNullException(nameof(continuation));

        ValidateToken(token);

        /* no use ValueTaskSourceOnCompletedFlags, always no capture ExecutionContext and SynchronizationContext. */

        /*
        PatternA: GetStatus=Pending => OnCompleted => TrySet*** => GetResult
        PatternB: TrySet*** => GetStatus=!Pending => GetResult
        PatternC: GetStatus=Pending => TrySet/OnCompleted(race condition) => GetResult
        C.1: win OnCompleted -> TrySet invoke saved continuation
        C.2: win TrySet -> should invoke continuation here.
    */
        object? oldContinuation = contRef;
        if (oldContinuation == null)
        {
            contState = state;
            oldContinuation = Interlocked.CompareExchange(ref contRef, continuation, null);
        }
        else
        {
            var wrapper = LightCallBackWrapper.Create(
                Unsafe.As<Action<object?, TweenEvent>>(oldContinuation),
                contState!,
                continuation,
                state);
            var newContinuation = Interlocked.CompareExchange(ref Unsafe.As<Action<object>, object>(ref contRef),
                (LightCallBackWrapper.RunAction), oldContinuation);
            if (ReferenceEquals(newContinuation, oldContinuation))
            {
                contState = wrapper;
                return;
            }

            wrapper.Release();
            oldContinuation = newContinuation;
        }

        if (oldContinuation != null)
        {
            // already running continuation in TrySet.
            // It will cause call OnCompleted multiple time, invalid.
            if (!ReferenceEquals(oldContinuation, TweenTaskCompletionLightSourceCoreShared.s_sentinel))
            {
                throw new InvalidOperationException(
                    "Already continuation registered, can not await twice or get Status after await.");
            }

            continuation(state);
        }
    }

    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateToken(short token)
    {
        if (token != Version)
            throw new InvalidOperationException(
                "Token version is not matched, can not await twice or get Status after await.");
    }
}

internal static class
    TweenTaskCompletionLightSourceCoreShared // separated out of generic to avoid unnecessary duplication
{
    internal static readonly Action<object?, TweenEvent> s_sentinel = CompletionSentinel;

    private static void CompletionSentinel(object? _, TweenEvent @event) // named method to aid debugging
    {
        throw new InvalidOperationException("The sentinel delegate should never be invoked.");
    }
}

internal class LightCallBackWrapper : ITaskPoolNode<LightCallBackWrapper>
{
    private static TaskPool<LightCallBackWrapper> pool;
    public Action<object?, TweenEvent> Callback = null!;
    public Action<object> Continuation = null!;
    public object ContinuationState = null!;
    public object State = null!;

    private LightCallBackWrapper? next = null;
    public ref LightCallBackWrapper? NextNode => ref next;

    public static LightCallBackWrapper Create(Action<object?, TweenEvent> callback, object state,
        Action<object> continuation, object continuationState)
    {
        if (!pool.TryPop(out var wrapper)) wrapper = new();

        wrapper.Callback = callback;
        wrapper.State = state;
        wrapper.Continuation = continuation;
        wrapper.ContinuationState = continuationState;
        return wrapper;
    }

    public static Action<object?, TweenEvent> RunAction = static (w, e) => Unsafe.As<LightCallBackWrapper>(w).Run(e);

    public void Run(TweenEvent tweenEvent)
    {
        var callback = Callback;
        var callbackState = State;
        var continuation = Continuation;
        var continuationState = ContinuationState;
        if (tweenEvent.EventType is TweenEventType.Complete or TweenEventType.Cancel)
        {
            Callback = null!;
            Continuation = null!;
            State = null!;
            ContinuationState = null!;
            pool.TryPush(this);
        }

        try
        {
            callback(callbackState, tweenEvent);
        }
        catch (Exception e)
        {
            try
            {
                TweenSystem.GetUnhandledExceptionHandler()(e);
            }
            catch
            {
                //
            }
        }
        finally
        {
            if (tweenEvent.EventType is TweenEventType.Complete or TweenEventType.Cancel)
            {
                continuation(continuationState);
            }
        }
    }

    public void Release()
    {
        Callback = null!;
        Continuation = null!;
        State = null!;
        ContinuationState = null!;
        pool.TryPush(this);
    }
}