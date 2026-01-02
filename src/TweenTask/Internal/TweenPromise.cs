using System;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace TweenTasks.Internal
{
    internal abstract class TweenPromise : IValueTaskSource
    {
        protected CancellationToken cancellationToken;
        protected TweenTaskCompletionSourceCore core;
        protected double delay;
        protected double duration;
        protected Ease ease;
        public double PlaybackSpeed;
        protected object? state;
        protected double time;

        public void GetResult(short token)
        {
            try
            {
                core.GetResult(token);
            }
            finally
            {
                TryReturn();
            }
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return core.GetStatus(token);
        }

        public void OnCompleted(Action<object> continuation, object state, short token,
            ValueTaskSourceOnCompletedFlags flags)
        {
            core.OnCompleted(continuation, state, token);
        }

        public abstract bool TryCancel(short token);
        public abstract bool TryComplete(short token);
        public abstract bool TryReturn();
    }

    internal class TweenPromise<T, TAdapter> : TweenPromise, ITweenRunnerWorkItem,
        ITaskPoolNode<TweenPromise<T, TAdapter>>
        where TAdapter : ITweenAdapter<T>
    {
        private static TaskPool<TweenPromise<T, TAdapter>> pool;
        private Action<object?, T> action = null!;
        private TAdapter adapter = default!;

        private TweenPromise<T, TAdapter>? next;
        ref TweenPromise<T, TAdapter>? ITaskPoolNode<TweenPromise<T, TAdapter>>.NextNode => ref next;

        public bool MoveNext(double deltaTime)
        {
            if (!core.IsActive) return false;

            time += PlaybackSpeed * deltaTime;
            var position = time - delay;
            var progress = Math.Min(1, position / duration);
            if (cancellationToken.IsCancellationRequested)
            {
                if (core.IsSetContinuationWithAwait)
                    core.TrySetCanceled(cancellationToken);
                else
                    ReturnWithContinuation(TweenResultType.Cancel);

                return false;
            }

            if (delay > time) return true;

            action(state, adapter.Evaluate(EaseUtility.Evaluate(progress, ease)));
            if (progress < 1) return true;

            adapter.Dispose();

            if (core.IsSetContinuationWithAwait)
            {
                core.TrySetResult();
                return false;
            }


            ReturnWithContinuation(TweenResultType.Complete);

            return false;
        }

        public static TweenPromise<T, TAdapter> Create(double delay, double duration, double playBackSpeed, Ease ease,
            TAdapter adapter,
            Action<object?, T> action, object? state, Action<object?, TweenResult>? endCallback, object? endState,
            CancellationToken cancellationToken, out short token)
        {
            if (!pool.TryPop(out var promise)) promise = new();

            promise.delay = delay;
            promise.duration = duration;
            promise.PlaybackSpeed = playBackSpeed;
            promise.ease = ease;
            promise.action = action;
            promise.state = state;
            promise.adapter = adapter;
            promise.cancellationToken = cancellationToken;
            promise.core.Activate();
            if (endCallback != null) promise.core.OnCompletedManual(endCallback, endState);
            promise.time = 0;
            token = promise.core.Version;
            return promise;
        }

        private void ReturnWithContinuation(TweenResultType result)
        {
            if (core.TryGetContinuation(out var continuation, out var continuationState))
            {
                TryReturn();
                continuation(continuationState, new(result));
            }
            else
            {
                TryReturn();
            }
        }

        public override bool TryCancel(short token)
        {
            if (core.Version != token) return false;
            adapter.Dispose();
            if (core.IsSetContinuationWithAwait)
            {
                core.Deactivate();
                core.TrySetCanceled(cancellationToken.IsCancellationRequested
                    ? cancellationToken
                    : CancellationToken.None);
            }
            else
            {
                ReturnWithContinuation(TweenResultType.Cancel);
            }


            return true;
        }


        public override bool TryComplete(short token)
        {
            if (core.Version != token) return false;
            action(state, adapter.Evaluate(EaseUtility.Evaluate(1, ease)));

            adapter.Dispose();

            if (core.IsSetContinuationWithAwait)
                core.TrySetResult();
            else
                ReturnWithContinuation(TweenResultType.Complete);

            return true;
        }


        public override bool TryReturn()
        {
            core.Reset();
            cancellationToken = CancellationToken.None;
            action = null!;
            //Console.WriteLine(GetType().Name+" is Returned");
            return pool.TryPush(this);
        }
    }
}