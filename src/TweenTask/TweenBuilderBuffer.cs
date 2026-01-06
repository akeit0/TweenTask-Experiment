using System;
using System.Threading;
using TweenTasks.Internal;

namespace TweenTasks;

internal sealed class TweenBuilderBuffer<TValue, TAdapter> : ITaskPoolNode<TweenBuilderBuffer<TValue, TAdapter>>,
    ITweenBuilderBuffer
    where TAdapter : ITweenAdapter<TValue>
{
    public TAdapter Adapter;

    public object? GetSetState;
    public Action<object?, TValue>? SetCallback;
    public CancellationToken CancellationToken;
    public double Delay;
    public double Duration;
    public int LoopCount = 1;
    public LoopType LoopType;
    public Ease Ease;
    public TweenTaskSettingFlagsWrapper Flags;
    public Action<object?, TweenEvent>? OnEndAction;
    public double PlaybackSpeed = 1;
    public object? OnEndState;
    public ushort Version;
    private static TaskPool<TweenBuilderBuffer<TValue, TAdapter>> taskPool;
    private TweenBuilderBuffer<TValue, TAdapter>? next;
    public ref TweenBuilderBuffer<TValue, TAdapter>? NextNode => ref next;
    public Func<object?, TValue>? GetCallback;

    public double TotalDuration => (Flags.HasFlag(TweenTaskSettingFlags.DelayEveryLoop) ? LoopCount : 1)*Delay + Duration * LoopCount;

    public static TweenBuilderBuffer<TValue, TAdapter> Rent()
    {
        if (!taskPool.TryPop(out var buffer)) buffer = new();
        return buffer;
    }

    public void ApplyAdapterState()
    {
        var isRelative = Flags.HasFlags(TweenTaskSettingFlags.IsRelative);
        if (GetCallback is not null)
        {
            Adapter.ApplyFrom(GetCallback(GetSetState), isRelative);
        }
        else if (isRelative)
        {
            Adapter.ApplyFrom(Adapter.From!, isRelative);
        }
    }

    public TweenPromise CreatePromise(out short token)
    {
        ApplyAdapterState();
        Flags.ClearFlags(TweenTaskSettingFlags.IsRelative);
        var promise = TweenPromise<TValue, TAdapter>.Create(Delay,
            Duration, PlaybackSpeed, LoopCount,LoopType, Ease, Adapter, SetCallback, GetSetState,
            OnEndAction, OnEndState,
            CancellationToken,(TweenTaskSettingFlags)Flags.Flags,
            out token);
        TryReturn();
        return promise;
    }

    public bool TryReturn()
    {
        PlaybackSpeed = 1;
        Adapter = default;
        LoopCount = 1;
        LoopType= default;
        Ease = default;
        Delay = 0;
        OnEndState = null;
        SetCallback = null;
        GetSetState = null;
        OnEndAction = null;
        Flags = default;
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
//         buffer.DeltaTimeProvider = ITweenDeltaTimeProvider.Default;
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
//         DeltaTimeProvider = null!;
//         Adapter = default;
//         if (Version != ushort.MaxValue) taskPool.TryPush(this);
//     }
// }