using System;
using System.Threading;
using TweenTasks.Internal;

namespace TweenTasks;

internal interface IReturnable{

    public bool TryReturn();
}
internal interface ITweenBuilderBuffer :IReturnable
{
    public TweenPromise CreatePromise(out short token);
    public double TotalDuration { get; }
}

internal sealed class TweenBuilderBuffer<TValue, TAdapter> : ITaskPoolNode<TweenBuilderBuffer<TValue, TAdapter>>,ITweenBuilderBuffer
    where TAdapter : ITweenAdapter<TValue>
{
    public TAdapter Adapter;

    public object? GetSetState;
    public Action<object?, TValue>? SetCallback;
    public CancellationToken CancellationToken;
    public double Delay;
    public double Duration;
    public Ease Ease;
    public bool IsRelative;
    public Action<object?, TweenResult>? OnEndAction;
    public double PlaybackSpeed = 1;
    public ITweenRunner Runner;
    public object? OnEndState;
    public ushort Version;
    private static TaskPool<TweenBuilderBuffer<TValue, TAdapter>> taskPool;
    private TweenBuilderBuffer<TValue, TAdapter>? next;
    public ref TweenBuilderBuffer<TValue, TAdapter>? NextNode => ref next;
    public Func<object?, TValue>? GetCallback;
    
    public double TotalDuration => Delay + Duration;

    public static TweenBuilderBuffer<TValue, TAdapter> Rent()
    {
        if (!taskPool.TryPop(out var buffer)) buffer = new();
        buffer.Runner = ITweenRunner.Default;
        return buffer;
    }

    public void ApplyAdapterState()
    {
        if (GetCallback is not null)
        {
            Adapter.ApplyFrom(GetCallback(GetSetState), IsRelative);
        }
        else if (IsRelative)
        {
            Adapter.ApplyFrom(Adapter.From!, IsRelative);
        }
    }

    public TweenPromise CreatePromise(out short token)
    {
        ApplyAdapterState();
        var promise = TweenPromise<TValue, TAdapter>.Create(Delay,
            Duration, PlaybackSpeed, Ease, Adapter, SetCallback, GetSetState,
            OnEndAction, OnEndState,
            CancellationToken,
            out  token);
        TryReturn();
        return promise;
    }

    public bool TryReturn()
    {
        PlaybackSpeed = 1;
        Runner = null!;
        Adapter = default;
        OnEndState = null;
        SetCallback = null;
        GetSetState = null;
        OnEndAction = null;
        if (Version != ushort.MaxValue) return taskPool.TryPush(this);
        return false;
    }
}

//
//
// internal sealed class TweenBuilderBufferWithFromGetter<TValue, TAdapter> : TweenBuilderBufferBase<TValue, TAdapter>,
//     ITaskPoolNode<TweenBuilderBufferWithFromGetter<TValue, TAdapter>>
//     where TAdapter : ITweenAdapter<TValue>, ITweenFromAdapter<TValue>
// {
//     private static TaskPool<TweenBuilderBufferWithFromGetter<TValue, TAdapter>> taskPool;
//   
//    
//     private TweenBuilderBufferWithFromGetter<TValue, TAdapter>? next;
//     public ref TweenBuilderBufferWithFromGetter<TValue, TAdapter>? NextNode => ref next;
//
//     public static TweenBuilderBufferWithFromGetter<TValue, TAdapter> Rent()
//     {
//         if (!taskPool.TryPop(out var buffer)) buffer = new();
//         buffer.Runner = ITweenRunner.Default;
//         return buffer;
//     }
//
//     public override void ApplyAdapterState()
//     {
//         if (GetCallback is not null)
//         {
//             Adapter.ApplyFrom(GetCallback(GetSetState),IsRelative);
//         }
//         else if(IsRelative)
//         {
//             Adapter.ApplyFrom(Adapter.From,IsRelative);
//             GetSetState = null;
//         }
//         GetSetState = null;
//     }
//
//     public override void Return()
//     {
//         PlaybackSpeed = 1;
//         Runner = null!;
//         Adapter = default;
//         if (Version != ushort.MaxValue) taskPool.TryPush(this);
//     }
// }