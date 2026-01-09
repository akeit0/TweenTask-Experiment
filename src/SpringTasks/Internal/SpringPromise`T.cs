using System;
using System.Diagnostics;
using System.Threading;
using MotionTasks;

namespace SpringTasks.Internal;

internal class SpringPromise<T, TAdapter> : SpringPromise, IFrameDeltaTimeProviderWorkItem,
    ITaskPoolNode<SpringPromise<T, TAdapter>>
    where TAdapter : ISpringAdapter<T>
{
    private static TaskPool<SpringPromise<T, TAdapter>> pool;
    private Action<object?, T>? action;
    private object? modifierState;
    private AdapterModifier<TAdapter, object?>? modifier;
    private TAdapter adapter = default!;

    private SpringPromise<T, TAdapter>? next;
    ref SpringPromise<T, TAdapter>? ITaskPoolNode<SpringPromise<T, TAdapter>>.NextNode => ref next;


    public static SpringPromise<T, TAdapter> Create(
        TAdapter adapter,
        Action<object?, T>? action, object? state, AdapterModifier<TAdapter, object?>? modifier, object? modifierState,
        Action<object?, SpringEvent>? endCallback, object? endState,
        CancellationToken cancellationToken, SpringTaskSettingFlags flags, out short token)
    {
        if (!pool.TryPop(out var promise))
        {
            promise = new();
        }

        promise.action = action;
        promise.modifierState = modifierState;
        promise.modifier = modifier;
        promise.State = state;
        promise.EventCallback = endCallback;
        promise.EventState = endState;
        promise.adapter = adapter;
        promise.CancellationToken = cancellationToken;
        promise.Core.Flags = new MotionTaskFlagsWrapper((int)flags);
        promise.Core.Activate();

        if (endCallback != null) promise.Core.HaveEvent = true;
        token = promise.Core.Version;
        return promise;
    }


    public bool MoveNext(FrameInfo frameInfo)
    {
        if (!Core.IsActive) return false;
        var deltaTime = frameInfo.DeltaTime;
        if (CancellationToken.IsCancellationRequested)
        {
            ReturnWithContinuation(new SpringEvent(SpringEventType.Cancel));

            return false;
        }

        if (modifier != null)
        {
            modifier(modifierState!, ref adapter);
        }

        var isCompleted = false;
        try
        {
            var result = adapter.Evaluate(deltaTime);
            if (adapter.IsCompleted)
            {
                isCompleted = true;
                result = adapter.Complete();
            }

            action?.Invoke(State, result);
        }
        catch (Exception e)
        {
            MotionSystem.GetUnhandledExceptionHandler()(e);
            if (Core.Flags.HasFlags(SpringTaskCompletionLightSourceFlags.CancelOnError))
            {
                ReturnWithContinuation(new SpringEvent(SpringEventType.Cancel));

                return false;
            }
        }

        if (!isCompleted) return true;

        ReturnWithContinuation(new SpringEvent(SpringEventType.Complete));

        return false;
    }


    public override bool TryComplete(short token)
    {
        if (Core.Version != token) return false;
        action?.Invoke(State, adapter.Complete());
        ReturnWithContinuation(new SpringEvent(SpringEventType.Complete));

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