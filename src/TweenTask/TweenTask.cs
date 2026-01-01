using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using TweenTasks.Internal;

namespace TweenTasks;

public readonly partial struct TweenTask
{
    public static TweenTask Create(double startTime, double endTime,Action<object?,double> callback,object? state, TweenRunner tweenRunner,
        CancellationToken cancellationToken)
    {
        var promise = TweenPromise.Create(startTime,
            startTime + endTime, callback,state, cancellationToken, out var token);
        tweenRunner.Register(promise);
        return new TweenTask(
            promise,
            token);
    }

    public static TweenTask Create(double endTime, Action<object?,double> callback,object? state, TweenRunner tweenRunner,
        CancellationToken cancellationToken = default)
    {
        var startTime = tweenRunner.GetCurrentTime();
        var promise = TweenPromise.Create(startTime,
            startTime + endTime, callback,state, cancellationToken, out var token);
        tweenRunner.Register(promise);
        return new TweenTask(
            promise,
            token);
    }
    public static TweenTask Create<TState>(double endTime, Action<TState,double> callback,TState state, TweenRunner tweenRunner,
        CancellationToken cancellationToken = default) where TState : class
    {
        return Create(endTime, Unsafe.As<Action<object?,double>>(callback), state, tweenRunner, cancellationToken);
    }
    
    readonly TweenPromise promise;
    private readonly short token;

    private TweenTask(TweenPromise promise, short token)
    {
        this.promise = promise;
        this.token = token;
    }

    public TweenTask WithOnComplete<TState>(Action<TState,TweenResultType> continuation, TState state) where TState : class
    {
        promise.OnCompletedManual(Unsafe.As<Action<object?,TweenResultType>>(continuation), state, token);
        return this;
    }
    
    public TweenTask WithOnComplete(Action<object?,TweenResultType> continuation, object? state) 
    {
        promise.OnCompletedManual(Unsafe.As<Action<object?,TweenResultType>>(continuation), state, token);
        return this;
    }

    public bool TryCancel()
    {
        return  promise.TryCancel(token);
    }

    public bool TryComplete()
    {
       return promise.TryComplete(token);
    }

    public ValueTask AsValueTask()
    {
        return new ValueTask(promise, token);
    }

    public void Forget(){}
    public ValueTaskAwaiter GetAwaiter()
    {
        return new ValueTask(promise, token).GetAwaiter();
    }
}

public enum TweenResultType:byte
{
    Complete,
    Cancel
}
