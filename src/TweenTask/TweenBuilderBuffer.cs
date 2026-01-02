using System;
using System.Threading;

namespace TweenTasks
{
    internal sealed class TweenBuilderBuffer<TValue, TAdapter> : ITaskPoolNode<TweenBuilderBuffer<TValue, TAdapter>>
        where TAdapter : ITweenAdapter<TValue>
    {
        private static TaskPool<TweenBuilderBuffer<TValue, TAdapter>> taskPool;
        public TAdapter Adapter;
        public CancellationToken CancellationToken;
        public double Delay;
        public double Duration;
        public Ease Ease;
        private TweenBuilderBuffer<TValue, TAdapter>? next;
        public Action<object?, TweenResult>? OnCompleteAction;
        public double PlaybackSpeed = 1;
        public ITweenRunner Runner;
        public object? State;
        public ushort Version;
        public ref TweenBuilderBuffer<TValue, TAdapter>? NextNode => ref next;

        public static TweenBuilderBuffer<TValue, TAdapter> Rent()
        {
            if (!taskPool.TryPop(out var buffer)) buffer = new();
            buffer.Runner = ITweenRunner.Default;
            return buffer;
        }

        public void Return()
        {
            PlaybackSpeed = 1;
            Runner = null!;
            Adapter = default;
            if (Version != ushort.MaxValue) taskPool.TryPush(this);
        }
    }
}