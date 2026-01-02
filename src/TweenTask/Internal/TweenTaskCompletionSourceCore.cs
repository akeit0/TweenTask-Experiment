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

namespace TweenTasks.Internal
{
    [Flags]
    public enum TweenTaskCompletionSourceFlags : byte
    {
        SetContinuationWithAwait = 1,
        Pooled = 2
    }

    [StructLayout(LayoutKind.Auto)]
    public struct TweenTaskCompletionSourceCore
    {
        private object? error; // ExceptionHolder or OperationCanceledException
        private bool hasUnhandledError;
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
            hasUnhandledError = false;
            continuation = null;
            continuationState = null;
            flags = TweenTaskCompletionSourceFlags.Pooled;
        }

        private void ReportUnhandledError()
        {
            if (hasUnhandledError)
                try
                {
                    if (error is OperationCanceledException oc)
                    {
                        //TweenTaskScheduler.PublishUnobservedTaskException(oc);
                    }
                    else if (error is ExceptionHolder e)
                    {
                        // TweenTaskScheduler.PublishUnobservedTaskException(e.GetException().SourceException);
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
            hasUnhandledError = false;
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
                    continuation(continuationState);

                return true;
            }

            return false;
        }

        /// <summary>Completes with a successful result.</summary>
        [DebuggerHidden]
        public bool TryGetContinuation(out Action<object?, TweenResultType> continuationAction,
            out object? continuationActionState)
        {
            Unsafe.SkipInit(out continuationAction);
            Unsafe.SkipInit(out continuationActionState);
            if (Interlocked.Increment(ref completedCount) == 1)
            {
                // setup result

                if (continuation != null ||
                    Interlocked.CompareExchange(ref continuation, TweenTaskCompletionSourceCoreShared.s_sentinel,
                        null) != null)
                {
                    continuationAction = Unsafe.As<Action<object?, TweenResultType>>(continuation);
                    continuationActionState = continuationState;
                }

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
                hasUnhandledError = true;
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
                hasUnhandledError = true;
                error = cancellationToken == CancellationToken.None
                    ? defaultCancelledException
                    : new(cancellationToken);

                if (continuation != null ||
                    Interlocked.CompareExchange(ref continuation, TweenTaskCompletionSourceCoreShared.s_sentinel,
                        null) != null)
                    continuation(continuationState);

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
                hasUnhandledError = false;
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

            /* no use ValueTaskSourceOnCOmpletedFlags, always no capture ExecutionContext and SynchronizationContext. */

            /*
            PatternA: GetStatus=Pending => OnCompleted => TrySet*** => GetResult
            PatternB: TrySet*** => GetStatus=!Pending => GetResult
            PatternC: GetStatus=Pending => TrySet/OnCompleted(race condition) => GetResult
            C.1: win OnCompleted -> TrySet invoke saved continuation
            C.2: win TrySet -> should invoke continuation here.
        */
            flags |= TweenTaskCompletionSourceFlags.SetContinuationWithAwait;
            // not set continuation yet.
            object oldContinuation = this.continuation;
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
                    throw new InvalidOperationException(
                        "Already continuation registered, can not await twice or get Status after await.");

                continuation(state);
            }
        }

        public void OnCompletedManual(Action<object?, TweenResultType> continuation, object? state,
            short token /*, ValueTaskSourceOnCompletedFlags flags */)
        {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));

            ValidateToken(token);

            /* no use ValueTaskSourceOnCOmpletedFlags, always no capture ExecutionContext and SynchronizationContext. */

            /*
            PatternA: GetStatus=Pending => OnCompleted => TrySet*** => GetResult
            PatternB: TrySet*** => GetStatus=!Pending => GetResult
            PatternC: GetStatus=Pending => TrySet/OnCompleted(race condition) => GetResult
            C.1: win OnCompleted -> TrySet invoke saved continuation
            C.2: win TrySet -> should invoke continuation here.
        */
            // not set continuation yet.
            object oldContinuation = this.continuation;
            if (oldContinuation == null)
            {
                continuationState = state;
                oldContinuation = Interlocked.CompareExchange(ref this.continuation,
                    Unsafe.As<Action<object>>(continuation), null);
            }

            if (oldContinuation != null)
            {
                // already running continuation in TrySet.
                // It will cause call OnCompleted multiple time, invalid.
                if (!ReferenceEquals(oldContinuation, TweenTaskCompletionSourceCoreShared.s_sentinel))
                    throw new InvalidOperationException(
                        "Already continuation registered, can not await twice or get Status after await.");

                continuation(state, TweenResultType.Cancel);
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
}