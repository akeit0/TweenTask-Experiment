using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using MotionTasks;

namespace MotionTasks;

public struct MotionTask
{
    public static ValueTask WaitWhile<TState>(TState state, Func<TState, bool> predicate,
        CancellationToken cancellationToken = default) where TState : class?
    {
        var promise = WaitWhilePromise.Create(state, Unsafe.As<Func<object?, bool>>(predicate), cancellationToken,
            out var token);
        MotionSystem.DefaultFrameDeltaTimeProvider.Register(promise);
        return new ValueTask(promise, token);
    }
}

public enum MotionEventType : byte
{
    Start,
    Complete,
    Cancel
}

internal class WaitWhilePromise : ITaskPoolNode<WaitWhilePromise>, IValueTaskSource, IFrameDeltaTimeProviderWorkItem
{
    static TaskPool<WaitWhilePromise> _taskPool;
    public ref WaitWhilePromise? NextNode => ref _nextNode;
    private WaitWhilePromise? _nextNode;

    Func<object?, bool>? _predicate;
    object? _state;
    CancellationToken _cancellationToken;
    ManualResetValueTaskSourceCore<bool> _core;

    public static WaitWhilePromise Create(object? state, Func<object?, bool> predicate,
        CancellationToken cancellationToken, out short token)
    {
        if (!_taskPool.TryPop(out var promise))
        {
            promise = new WaitWhilePromise();
        }

        promise._predicate = predicate;
        promise._state = state;
        promise._cancellationToken = cancellationToken;
        token = promise._core.Version;
        return promise;
    }


    public void GetResult(short token)
    {
        try
        {
            _core.GetResult(token);
        }
        finally
        {
            Return();
        }
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _core.GetStatus(token);
    }

    public void OnCompleted(Action<object> continuation, object state, short token,
        ValueTaskSourceOnCompletedFlags flags)
    {
        _core.OnCompleted(continuation, state, token, flags);
    }

    public bool MoveNext(FrameInfo info)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            _core.SetException(new OperationCanceledException(_cancellationToken));
            return false;
        }

        if (_predicate != null && _predicate(_state))
        {
            return true;
        }

        _core.SetResult(true);
        return false;
    }

    private void Return()
    {
        _predicate = null;
        _state = null;
        _cancellationToken = default;
        _core.Reset();
        _taskPool.TryPush(this);
    }
}