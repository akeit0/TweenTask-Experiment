using System;
using System.Threading;
using SpringTasks.Internal;
using MotionTasks;

namespace SpringTasks;

internal sealed class SpringBuilderBuffer<TValue, TAdapter> : ITaskPoolNode<SpringBuilderBuffer<TValue, TAdapter>>
    where TAdapter : ISpringAdapter<TValue>
{
    public TAdapter Adapter;

    public object? GetSetState;
    public Action<object?, TValue>? SetCallback;
    public CancellationToken CancellationToken;
    public SpringTaskSettingFlagsWrapper Flags;
    public Action<object?, SpringEvent>? OnEndAction;
    public object? OnEndState;
    public object? ToGetterState;
    public AdapterModifier<TAdapter,object>? Modifier;
    public ushort Version;
    private static TaskPool<SpringBuilderBuffer<TValue, TAdapter>> taskPool;
    private SpringBuilderBuffer<TValue, TAdapter>? next;
    public ref SpringBuilderBuffer<TValue, TAdapter>? NextNode => ref next;
    public Func<object?, TValue>? GetCallback;

    public static SpringBuilderBuffer<TValue, TAdapter> Rent()
    {
        if (!taskPool.TryPop(out var buffer)) buffer = new();
        return buffer;
    }

    public void ApplyAdapterState()
    {
        var isRelative = Flags.HasFlags(SpringTaskSettingFlags.IsRelative);
        if (GetCallback is not null)
        {
            Adapter.ApplyFrom(GetCallback(GetSetState), isRelative);
        }
        else if (isRelative)
        {
            Adapter.ApplyFrom(Adapter.From!, isRelative);
        }
    }

    public SpringPromise CreatePromise(out short token)
    {
        ApplyAdapterState();
        Flags.ClearFlags(SpringTaskSettingFlags.IsRelative);
        var promise = SpringPromise<TValue, TAdapter>.Create(Adapter, SetCallback, GetSetState, Modifier, ToGetterState,
            OnEndAction, OnEndState,
            CancellationToken, (SpringTaskSettingFlags)Flags.Flags,
            out token);
        TryReturn();
        return promise;
    }

    public bool TryReturn()
    {
        Adapter = default;
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
// internal sealed class SpringBuilderBufferWithFromGetter<TValue, TAdapter> : SpringBuilderBufferBase<TValue, TAdapter>,
//     ITaskPoolNode<SpringBuilderBufferWithFromGetter<TValue, TAdapter>>
//     where TAdapter : ISpringAdapter<TValue>, ISpringFromAdapter<TValue>
// {
//     private static TaskPool<SpringBuilderBufferWithFromGetter<TValue, TAdapter>> taskPool;
//   
//    
//     private SpringBuilderBufferWithFromGetter<TValue, TAdapter>? next;
//     public ref SpringBuilderBufferWithFromGetter<TValue, TAdapter>? NextNode => ref next;
//
//     public static SpringBuilderBufferWithFromGetter<TValue, TAdapter> Rent()
//     {
//         if (!taskPool.TryPop(out var buffer)) buffer = new();
//         buffer.DeltaTimeProvider = ISpringDeltaTimeProvider.Default;
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