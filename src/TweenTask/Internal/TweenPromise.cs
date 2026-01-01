using System;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace TweenTasks.Internal;

internal class TweenPromise : IValueTaskSource, ITweenRunnerWorkItem, ITaskPoolNode<TweenPromise>
{
    CancellationToken cancellationToken;
    TweenTaskCompletionSourceCore core;
    Action<object?, double> action;
    object? state;
    double startTime;
    double endTime;

    static TaskPool<TweenPromise> pool;

    private TweenPromise next;
    ref TweenPromise ITaskPoolNode<TweenPromise>.NextNode => ref next;

    private TweenPromise()
    {
    }

    public static TweenPromise Create(double startTime, double endTime, Action<object?, double> action, object? state,
        CancellationToken cancellationToken, out short token)
    {
        if (!pool.TryPop(out var promise))
        {
            promise = new TweenPromise();
        }

        promise.startTime = startTime;
        promise.endTime = endTime;
        promise.action = action;
        promise.state = state;
        promise.cancellationToken = cancellationToken;
        promise.core.Activate();
        token = promise.core.Version;

        return promise;
    }

    public bool MoveNext(double currentTime)
    {
        if (!core.IsActive)
        {
            return false;
        }

        var position = currentTime - startTime;
        var duration = endTime - startTime;
        var progress = Math.Min(1, position / duration);
        if (cancellationToken.IsCancellationRequested)
        {
            if (core.IsSetContinuationWithAwait)
            {
                core.TrySetCanceled(cancellationToken);
            }
            else
            {
                ReturnWithContinuation(TweenResultType.Cancel);
            }

            return false;
        }

        if (startTime > currentTime) return true;

        action(state, progress);
        if (currentTime < endTime)
        {
            return true;
        }

        if (core.IsSetContinuationWithAwait)
        {
            core.TrySetResult();
            return false;
        }

        ReturnWithContinuation(TweenResultType.Complete);

        return false;
    }

    void ReturnWithContinuation(TweenResultType result)
    {
        if (core.TryGetContinuation(out var continuation, out var continuationState))
        {
            TryReturn();
            continuation(continuationState, result);
        }
    }

    public bool TryCancel(short token)
    {
        if (core.Version != token) return false;

        if (core.IsSetContinuationWithAwait)
        {
            core.Deactivate();
            core.TrySetCanceled(cancellationToken.IsCancellationRequested ? cancellationToken : CancellationToken.None);
        }
        else
        {
            ReturnWithContinuation(TweenResultType.Cancel);
        }


        return true;
    }


    public bool TryComplete(short token)
    {
        if (core.Version != token) return false;
        action(state, 1);
        if (core.IsSetContinuationWithAwait)
        {
            core.TrySetResult();
        }
        else
        {
            ReturnWithContinuation(TweenResultType.Complete);
        }

        return true;
    }

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

    public void OnCompletedManual(Action<object?, TweenResultType> continuation, object? state, short token)
    {
        core.OnCompletedManual(continuation, state, token);
    }

    bool TryReturn()
    {
        core.Reset();
        cancellationToken = CancellationToken.None;
        action = null!;
        return pool.TryPush(this);
    }
}