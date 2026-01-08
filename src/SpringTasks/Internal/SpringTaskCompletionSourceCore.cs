using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks.Sources;
using MotionTasks;
using MotionTasks.Internal;

namespace SpringTasks.Internal;

[Flags]
internal enum SpringTaskSettingFlags
{
    None = 0,
    Preserved = 1,
    ApplyValuesDuringDelay = Preserved << 1,
    DelayEveryLoop = ApplyValuesDuringDelay << 1,
    CancelOnError = DelayEveryLoop << 1,
    ThrowOnManuallyCanceled = CancelOnError << 1,
    ThrowBindActionException = ThrowOnManuallyCanceled << 1,
    IsRelative,
}

[Flags]
internal enum SpringTaskCompletionLightSourceFlags
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

internal struct SpringTaskSettingFlagsWrapper(int flags)
{
    public int Flags = flags;

    public bool TrySetFlags(SpringTaskSettingFlags setFlags)
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

    public bool HasFlags(SpringTaskSettingFlags checkFlags)
    {
        return (Flags & (int)checkFlags) != 0;
    }

    public bool HasFlag(SpringTaskSettingFlags checkFlags)
    {
        return (Flags & (int)checkFlags) != 0;
    }

    public void SetFlags(SpringTaskSettingFlags setFlags)
    {
        Flags |= (int)setFlags;
    }

    public void ClearFlags(SpringTaskSettingFlags clearFlags)
    {
        Flags &= ~(int)clearFlags;
    }
}

internal struct MotionTaskFlagsWrapper(int flags)
{
    public int Flags = flags;

    public bool TrySetFlags(SpringTaskCompletionLightSourceFlags setFlags)
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

    public bool HasFlags(SpringTaskCompletionLightSourceFlags checkFlags)
    {
        return (Flags & (int)checkFlags) != 0;
    }

    public void SetFlags(SpringTaskCompletionLightSourceFlags setFlags)
    {
        Flags |= (int)setFlags;
    }

    public void ClearFlags(SpringTaskCompletionLightSourceFlags clearFlags)
    {
        Flags &= ~(int)clearFlags;
    }
}

[StructLayout(LayoutKind.Auto)]
public struct MotionTaskCompletionSourceLightCore
{
    private object? error; // ExceptionHolder or OperationCanceledException
    internal MotionTaskFlagsWrapper Flags;

    /// <summary>Gets the operation version.</summary>
    [DebuggerHidden]
    public short Version { get; private set; }

    public void Activate()
    {
        Flags.SetFlags(SpringTaskCompletionLightSourceFlags.Pooled);
    }

    public void Deactivate()
    {
        Flags.SetFlags(SpringTaskCompletionLightSourceFlags.Pooled);
    }

    public bool IsActive => Flags.HasFlags(SpringTaskCompletionLightSourceFlags.Pooled);

    public bool IsPreserved
    {
        get => Flags.HasFlags(SpringTaskCompletionLightSourceFlags.Preserved);
        set
        {
            if (value)
            {
                Flags.SetFlags(SpringTaskCompletionLightSourceFlags.Preserved);
            }
            else
            {
                Flags.ClearFlags(SpringTaskCompletionLightSourceFlags.Preserved);
            }
        }
    }

    internal void MarkHandled()
    {
        Flags.ClearFlags(SpringTaskCompletionLightSourceFlags.HasHandledError);
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
        Flags = new MotionTaskFlagsWrapper((int)SpringTaskCompletionLightSourceFlags.Pooled);
    }

    private void ReportUnhandledError()
    {
        if (Flags.HasFlags(SpringTaskCompletionLightSourceFlags.HasHandledError))
            try
            {
                if (error is OperationCanceledException oc)
                {
                    MotionSystem.GetUnhandledExceptionHandler().Invoke(oc);
                }
                else if (error is ExceptionHolder e)
                {
                    MotionSystem.GetUnhandledExceptionHandler().Invoke(e.GetException().SourceException);
                }
            }
            catch
            {
            }
    }

    public bool HaveEvent
    {
        get => Flags.HasFlags(SpringTaskCompletionLightSourceFlags.HasEvent);
        set
        {
            if (value)
            {
                Flags.SetFlags(SpringTaskCompletionLightSourceFlags.HasEvent);
            }
            else
            {
                Flags.ClearFlags(SpringTaskCompletionLightSourceFlags.HasEvent);
            }
        }
    }

    /// <summary>Completes with a successful result.</summary>
    [DebuggerHidden]
    public bool TrySet()
    {
        return Flags.TrySetFlags(SpringTaskCompletionLightSourceFlags.Done);
    }

    public void SetCanceledException(CancellationToken cancellationToken)
    {
        Flags.SetFlags(SpringTaskCompletionLightSourceFlags.HasHandledError);
        error = cancellationToken == CancellationToken.None
            ? defaultCancelledException
            : new OperationCanceledException(cancellationToken);
    }

    public void RunContinuation(Action<object?> continuation, object? continuationState)
    {
        if (HaveEvent)
        {
            continuation!(continuationState);
        }
        else
        {
            Unsafe.As<Action<object>>(continuation)(continuationState!);
        }
    }


    private static readonly OperationCanceledException defaultCancelledException = new(CancellationToken.None);


    /// <summary>Gets the status of the operation.</summary>
    /// <param name="token">Opaque value that was provided to the <see cref="MotionTask" />'s constructor.</param>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTaskSourceStatus GetStatus(object? continuation, short token)
    {
        ValidateToken(token);
        return continuation == null || (!Flags.HasFlags(SpringTaskCompletionLightSourceFlags.Done))
            ? ValueTaskSourceStatus.Pending
            : error == null
                ? ValueTaskSourceStatus.Succeeded
                : error is OperationCanceledException
                    ? ValueTaskSourceStatus.Canceled
                    : ValueTaskSourceStatus.Faulted;
    }

    /// <summary>Gets the result of the operation.</summary>
    /// <param name="token">Opaque value that was provided to the <see cref="MotionTask" />'s constructor.</param>
    // [StackTraceHidden]
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetResult(short token)
    {
        ValidateToken(token);

        if (!Flags.HasFlags(SpringTaskCompletionLightSourceFlags.Done))
            throw new InvalidOperationException("Not yet completed, MotionTask only allow to use await.");

        if (error != null)
        {
            Flags.ClearFlags(SpringTaskCompletionLightSourceFlags.HasHandledError);
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
    /// <param name="token">Opaque value that was provided to the <see cref="MotionTask" />'s constructor.</param>
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
                Unsafe.As<Action<object?,SpringEvent>>(oldContinuation),
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
            if (!ReferenceEquals(oldContinuation, MotionTaskCompletionLightSourceCoreShared.s_sentinel))
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
    MotionTaskCompletionLightSourceCoreShared // separated out of generic to avoid unnecessary duplication
{
    internal static readonly Action<object?, SpringEvent> s_sentinel = CompletionSentinel;

    private static void CompletionSentinel(object? _, SpringEvent @event) // named method to aid debugging
    {
        throw new InvalidOperationException("The sentinel delegate should never be invoked.");
    }
}

internal class LightCallBackWrapper : ITaskPoolNode<LightCallBackWrapper>
{
    private static TaskPool<LightCallBackWrapper> pool;
    public Action<object?, SpringEvent> Callback = null!;
    public Action<object> Continuation = null!;
    public object ContinuationState = null!;
    public object State = null!;

    private LightCallBackWrapper? next = null;
    public ref LightCallBackWrapper? NextNode => ref next;

    public static LightCallBackWrapper Create(Action<object?, SpringEvent> callback, object state,
        Action<object> continuation, object continuationState)
    {
        if (!pool.TryPop(out var wrapper)) wrapper = new();

        wrapper.Callback = callback;
        wrapper.State = state;
        wrapper.Continuation = continuation;
        wrapper.ContinuationState = continuationState;
        return wrapper;
    }

    public static readonly Action<object?, SpringEvent> RunAction = static (w, e) =>
        Unsafe.As<LightCallBackWrapper>(w).Run(e);

    public void Run(SpringEvent SpringEvent)
    {
        var callback = Callback;
        var callbackState = State;
        var continuation = Continuation;
        var continuationState = ContinuationState;
        if (SpringEvent.EventType is SpringEventType.Complete or SpringEventType.Cancel)
        {
            Callback = null!;
            Continuation = null!;
            State = null!;
            ContinuationState = null!;
            pool.TryPush(this);
        }

        try
        {
            callback(callbackState, SpringEvent);
        }
        catch (Exception e)
        {
            try
            {
                MotionSystem.GetUnhandledExceptionHandler()(e);
            }
            catch
            {
                //
            }
        }
        finally
        {
            if (SpringEvent.EventType is SpringEventType.Complete or SpringEventType.Cancel)
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