/*
MIT License
Copyright (c) Cysharp, Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace TweenTasks.Internal;

[Flags]
public enum TweenTaskCompletionSourceFlags
{
    SetContinuationWithAwait = 1,
    Pooled = 2,
    Wrapped = 4,
    HasHandledError = 8,
    Preserved = 16,
}

[StructLayout(LayoutKind.Auto)]
public struct TweenTaskCompletionSourceCore
{
    private object? error; // ExceptionHolder or OperationCanceledException
    private TweenTaskCompletionSourceFlags flags;
    private int completedCount; // 0: completed == false
    private Action<object?>? continuation;
    private object? continuationState;

    public void Activate()
    {
        flags &= ~TweenTaskCompletionSourceFlags.Pooled;
    }

    public void Deactivate()
    {
        flags |= TweenTaskCompletionSourceFlags.Pooled;
    }

    public bool IsActive => (flags & TweenTaskCompletionSourceFlags.Pooled) == 0;

    public bool IsPreserved
    {
        get => (flags & TweenTaskCompletionSourceFlags.Preserved) != 0;
        set
        {
            if (value)
            {
                flags |= TweenTaskCompletionSourceFlags.Preserved;
            }
            else
            {
                flags &= ~TweenTaskCompletionSourceFlags.Preserved;
            }
        }
    }

    [DebuggerHidden]
    public void Reset()
    {
        ReportUnhandledError();

        unchecked
        {
            Version += 1; // incr version.
        }

        completedCount = 0;
        error = null;
        continuation = null;
        continuationState = null;
        flags = TweenTaskCompletionSourceFlags.Pooled;
    }

    private void ReportUnhandledError()
    {
        if ((flags & TweenTaskCompletionSourceFlags.HasHandledError) != 0)
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

    public bool IsSetContinuationWithAwait =>
        (flags & TweenTaskCompletionSourceFlags.SetContinuationWithAwait) != 0;

    internal void MarkHandled()
    {
        flags &= ~TweenTaskCompletionSourceFlags.HasHandledError;
    }

    /// <summary>Completes with a successful result.</summary>
    [DebuggerHidden]
    public bool TrySetResult()
    {
        if (Interlocked.Increment(ref completedCount) == 1)
        {
            // setup result
            if (continuation != null ||
                Interlocked.CompareExchange(ref continuation, TweenTaskCompletionSourceCoreShared.s_sentinel,
                    null) != null)
                if ((flags & TweenTaskCompletionSourceFlags.Wrapped) != 0)
                    Unsafe.As<CallBackWrapper>(continuationState)
                        .Run(TweenEventType.Complete);
                else continuation(continuationState);

            return true;
        }

        return false;
    }

    /// <summary>Completes with a successful result.</summary>
    [DebuggerHidden]
    public bool TryGetContinuation(out Action<object?, TweenEvent> continuationAction,
        out object? continuationActionState)
    {
        Unsafe.SkipInit(out continuationAction);
        Unsafe.SkipInit(out continuationActionState);
        if (Interlocked.Increment(ref completedCount) == 1)
            // setup result
            if (continuation != null ||
                Interlocked.CompareExchange(ref continuation, TweenTaskCompletionSourceCoreShared.s_sentinel,
                    null) != null)
            {
                continuationAction = Unsafe.As<Action<object?, TweenEvent>>(continuation);
                continuationActionState = continuationState;
                return true;
            }

        return false;
    }

    /// <summary>Completes with an error.</summary>
    /// <param name="error">The exception.</param>
    [DebuggerHidden]
    public bool TrySetException(Exception error)
    {
        if (Interlocked.Increment(ref completedCount) == 1)
        {
            // setup result
            flags |= TweenTaskCompletionSourceFlags.HasHandledError;
            if (error is OperationCanceledException)
                this.error = error;
            else
                this.error = new ExceptionHolder(ExceptionDispatchInfo.Capture(error));

            if (continuation != null ||
                Interlocked.CompareExchange(ref continuation, TweenTaskCompletionSourceCoreShared.s_sentinel,
                    null) != null)
                continuation(continuationState);

            return true;
        }

        return false;
    }

    private static readonly OperationCanceledException defaultCancelledException = new(CancellationToken.None);

    [DebuggerHidden]
    public bool TrySetCanceled(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref completedCount) == 1)
        {
            // setup result
            flags |= TweenTaskCompletionSourceFlags.HasHandledError;
            error = new OperationCanceledException(cancellationToken);

            if (continuation != null ||
                Interlocked.CompareExchange(ref continuation, TweenTaskCompletionSourceCoreShared.s_sentinel,
                    null) != null)

                if ((flags & TweenTaskCompletionSourceFlags.Wrapped) != 0)
                    Unsafe.As<CallBackWrapper>(continuationState)
                        .Run(TweenEventType.Cancel);
                else continuation(continuationState);

            return true;
        }

        return false;
    }

    /// <summary>Gets the operation version.</summary>
    [DebuggerHidden]
    public short Version { get; private set; }

    /// <summary>Gets the status of the operation.</summary>
    /// <param name="token">Opaque value that was provided to the <see cref="TweenTask" />'s constructor.</param>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTaskSourceStatus GetStatus(short token)
    {
        ValidateToken(token);
        return continuation == null || completedCount == 0 ? ValueTaskSourceStatus.Pending
            : error == null ? ValueTaskSourceStatus.Succeeded
            : error is OperationCanceledException ? ValueTaskSourceStatus.Canceled
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

        if (completedCount == 0)
            throw new InvalidOperationException("Not yet completed, TweenTask only allow to use await.");

        if (error != null)
        {
            flags &= ~TweenTaskCompletionSourceFlags.HasHandledError;
            if (error is OperationCanceledException oce) throw oce;

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
        short token /*, ValueTaskSourceOnCompletedFlags flags */)
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
        var oldFlags = (int)flags;
        if ((oldFlags & (int)TweenTaskCompletionSourceFlags.SetContinuationWithAwait) != 0 ||
            Interlocked.CompareExchange(ref Unsafe.As<TweenTaskCompletionSourceFlags, int>(ref flags), oldFlags |
                (int)TweenTaskCompletionSourceFlags.SetContinuationWithAwait,
                oldFlags) != oldFlags)
            throw new InvalidOperationException(
                "Already continuation registered, can not await twice or get Status after await.");

        // not set continuation yet.
        object? oldContinuation = this.continuation;
        if (oldContinuation == null)
        {
            continuationState = state;
            oldContinuation = Interlocked.CompareExchange(ref this.continuation, continuation, null);
        }

        if (oldContinuation != null)
        {
            // already running continuation in TrySet.
            // It will cause call OnCompleted multiple time, invalid.
            if (!ReferenceEquals(oldContinuation, TweenTaskCompletionSourceCoreShared.s_sentinel))
            {
                var wrapper = CallBackWrapper.Create(Unsafe.As<Action<object?, TweenEvent>>(oldContinuation),
                    continuationState!, continuation, state);
                this.continuation = static _ => { };
                continuationState = wrapper;
                flags |= TweenTaskCompletionSourceFlags.Wrapped;
                return;
            }

            continuation(state);
        }
    }

    public void OnCompletedManual(Action<object?, TweenEvent> continuation, object? state)
    {
        if (continuation == null) throw new ArgumentNullException(nameof(continuation));
        this.continuation = Unsafe.As<Action<object?>>(continuation);
        continuationState = state;
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

[Flags]
public enum TweenTaskCompletionLightSourceFlags
{
    HaveEvent = 1,
    Pooled = 2,
    Done = 4,
    HasHandledError = 8,
    Preserved = 16,
}

[StructLayout(LayoutKind.Auto)]
public struct TweenTaskCompletionSourceLightCore
{
    private object? error; // ExceptionHolder or OperationCanceledException
    private TweenTaskCompletionLightSourceFlags flags;

    public void Activate()
    {
        flags &= ~TweenTaskCompletionLightSourceFlags.Pooled;
    }

    public void Deactivate()
    {
        flags |= TweenTaskCompletionLightSourceFlags.Pooled;
    }

    public bool IsActive => (flags & TweenTaskCompletionLightSourceFlags.Pooled) == 0;

    public bool IsPreserved
    {
        get => (flags & TweenTaskCompletionLightSourceFlags.Preserved) != 0;
        set
        {
            if (value)
            {
                flags |= TweenTaskCompletionLightSourceFlags.Preserved;
            }
            else
            {
                flags &= ~TweenTaskCompletionLightSourceFlags.Preserved;
            }
        }
    }

    internal void MarkHandled()
    {
        flags &= ~TweenTaskCompletionLightSourceFlags.HasHandledError;
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
        flags = TweenTaskCompletionLightSourceFlags.Pooled;
    }

    private void ReportUnhandledError()
    {
        if ((flags & TweenTaskCompletionLightSourceFlags.HasHandledError) != 0)
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
        get => (flags & TweenTaskCompletionLightSourceFlags.HaveEvent) != 0;
        set
        {
            if (value)
            {
                flags |= TweenTaskCompletionLightSourceFlags.HaveEvent;
            }
            else
            {
                flags &= ~TweenTaskCompletionLightSourceFlags.HaveEvent;
            }
        }
    }

    /// <summary>Completes with a successful result.</summary>
    [DebuggerHidden]
    public bool TrySetResult(ref Action<object?, TweenEvent>? continuation, ref object continuationState,
        TweenEvent tweenEvent)
    {
        ref var flagRef = ref Unsafe.As<TweenTaskCompletionLightSourceFlags, int>(ref flags);
        var flagVal = flagRef;
        while (Interlocked.CompareExchange(ref flagRef, flagVal | (int)TweenTaskCompletionLightSourceFlags.Done,
                   flagVal) != flagVal)
        {
            flagVal = flagRef;
            if ((flagRef & (int)TweenTaskCompletionLightSourceFlags.Done) != 0)
                return false;
        }

        // setup result
        if (continuation != null ||
            Interlocked.CompareExchange(ref continuation, TweenTaskCompletionLightSourceCoreShared.s_sentinel,
                null) != null)
        {
            if (HaveEvent)
            {
                continuation(continuationState, tweenEvent);
            }
            else
            {
                Unsafe.As<Action<object>>(continuation)(continuationState);
            }
        }

        return true;
    }

    [DebuggerHidden]
    public bool TrySetCanceled(ref Action<object?, TweenEvent>? continuation, ref object continuationState,
        CancellationToken cancellationToken)
    {
        ref var flagRef = ref Unsafe.As<TweenTaskCompletionLightSourceFlags, int>(ref flags);
        var flagVal = flagRef;
        while (Interlocked.CompareExchange(ref flagRef, flagVal | (int)TweenTaskCompletionLightSourceFlags.Done,
                   flagVal) != flagVal)
        {
            flagVal = flagRef;
            if ((flagRef & (int)TweenTaskCompletionLightSourceFlags.Done) != 0)
                return false;
        }

        flags |= TweenTaskCompletionLightSourceFlags.HasHandledError;
        error = cancellationToken == CancellationToken.None
            ? defaultCancelledException
            : new OperationCanceledException(cancellationToken);

        // setup result
        if (continuation != null ||
            Interlocked.CompareExchange(ref continuation, TweenTaskCompletionLightSourceCoreShared.s_sentinel,
                null) != null)
        {
            if (HaveEvent)
            {
                continuation(continuationState, new TweenEvent(TweenEventType.Cancel));
            }
            else
            {
                Unsafe.As<Action<object>>(continuation)(continuationState);
            }
        }

        return true;
    }


    private static readonly OperationCanceledException defaultCancelledException = new(CancellationToken.None);

    /// <summary>Gets the operation version.</summary>
    [DebuggerHidden]
    public short Version { get; private set; }

    /// <summary>Gets the status of the operation.</summary>
    /// <param name="token">Opaque value that was provided to the <see cref="TweenTask" />'s constructor.</param>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTaskSourceStatus GetStatus(object? continuation, short token)
    {
        ValidateToken(token);
        return continuation == null || (flags & TweenTaskCompletionLightSourceFlags.Done) == 0
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

        if ((flags & TweenTaskCompletionLightSourceFlags.Done) == 0)
            throw new InvalidOperationException("Not yet completed, TweenTask only allow to use await.");

        if (error != null)
        {
            flags &= ~TweenTaskCompletionLightSourceFlags.HasHandledError;
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

internal class CallBackWrapper : ITaskPoolNode<CallBackWrapper>
{
    private static TaskPool<CallBackWrapper> pool;
    public Action<object?, TweenEvent> Callback = null!;
    public Action<object> Continuation = null!;
    public object ContinuationState = null!;
    private CallBackWrapper? next = null;
    public object State = null!;
    public ref CallBackWrapper? NextNode => ref next;

    public static CallBackWrapper Create(Action<object?, TweenEvent> callback, object state,
        Action<object> continuation, object continuationState)
    {
        if (!pool.TryPop(out var wrapper)) wrapper = new();

        wrapper.Callback = callback;
        wrapper.State = state;
        wrapper.Continuation = continuation;
        wrapper.ContinuationState = continuationState;
        return wrapper;
    }


    public void Run(TweenEventType eventType)
    {
        var callback = Callback;
        var callbackState = State;
        var continuation = Continuation;
        var continuationState = ContinuationState;
        Callback = null!;
        Continuation = null!;
        State = null!;
        ContinuationState = null!;
        pool.TryPush(this);
        try
        {
            callback(callbackState, new(eventType));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            continuation(continuationState);
        }
    }
}