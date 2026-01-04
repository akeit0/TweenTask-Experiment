using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TweenTasks.Internal;

namespace TweenTasks;

public static class TweenBuilder
{
    public static TweenBuilderEntry<TValue, TAdapter> CreateEntry<TValue, TAdapter>(TAdapter adapter, double duration)
        where TAdapter : ITweenAdapter<TValue>, ITweenFromAdapter<TValue>
    {
        return new TweenBuilderEntry<TValue, TAdapter>(adapter, duration);
    }

    public static TweenToBuilderEntry<TValue, TAdapter> CreateToEntry<TValue, TAdapter>(TAdapter adapter,
        double duration)
        where TAdapter : ITweenAdapter<TValue>, ITweenFromAdapter<TValue>
    {
        return new TweenToBuilderEntry<TValue, TAdapter>(adapter, duration);
    }

    extension<TValue, TAdapter>(TweenBuilderEntry<TValue, TAdapter> builderEntry) where TAdapter : ITweenAdapter<TValue>
    {
        public TweenBuilder<TValue, TAdapter> Bind<TState>(TState state, Action<TState, TValue> callback)
            where TState : class
        {
            var buffer = TweenBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.Adapter = builderEntry.Adapter;
            buffer.Duration = builderEntry.Duration;
            buffer.GetSetState = state;
            buffer.SetCallback = Unsafe.As<Action<object?, TValue>>(callback);
            return new(buffer, buffer.Version);
        }

        public TweenBuilder<TValue, TAdapter> Bind<TState>(TState state, Action<TState, TValue> callback,
            CancellationToken cancellationToken) where TState : class
        {
            var buffer = TweenBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.CancellationToken = cancellationToken;
            buffer.Adapter = builderEntry.Adapter;
            buffer.Duration = builderEntry.Duration;
            buffer.GetSetState = state;
            buffer.SetCallback = Unsafe.As<Action<object?, TValue>>(callback);
            return new(buffer, buffer.Version);
        }

        public TweenBuilder<TValue, TAdapter> Bind(Action<TValue> callback)
        {
            var buffer = TweenBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.Adapter = builderEntry.Adapter;
            buffer.Duration = builderEntry.Duration;
            buffer.GetSetState = callback;
            buffer.SetCallback = static (o, value) => { Unsafe.As<Action<TValue>>(o)(value); };
            return new(buffer, buffer.Version);
        }

        public TweenBuilder<TValue, TAdapter> Bind(Action<TValue> callback, CancellationToken cancellationToken)
        {
            var buffer = TweenBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.CancellationToken = cancellationToken;
            buffer.Adapter = builderEntry.Adapter;
            buffer.Duration = builderEntry.Duration;
            buffer.GetSetState = callback;
            buffer.SetCallback = static (o, value) => { Unsafe.As<Action<TValue>>(o)(value); };
            return new(buffer, buffer.Version);
        }
    }

    extension<TValue, TAdapter>(TweenToBuilderEntry<TValue, TAdapter> builderEntry)
        where TAdapter : ITweenAdapter<TValue>, ITweenFromAdapter<TValue>
    {
        public TweenBuilder<TValue, TAdapter> Bind<TState>(TState state, Func<TState, TValue> getCallback,
            Action<TState, TValue> setCallback) where TState : class
        {
            var buffer = TweenBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.Adapter = builderEntry.Adapter;
            buffer.Duration = builderEntry.Duration;
            buffer.GetSetState = state;
            buffer.GetCallback = Unsafe.As<Func<object?, TValue>>(getCallback);
            buffer.SetCallback = Unsafe.As<Action<object?, TValue>>(setCallback);
            return new(buffer, buffer.Version);
        }

        public TweenBuilder<TValue, TAdapter> Bind<TState>(TState state, Func<TState, TValue> getCallback,
            Action<TState, TValue> setCallback, CancellationToken cancellationToken) where TState : class
        {
            var buffer = TweenBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.CancellationToken = cancellationToken;
            buffer.Adapter = builderEntry.Adapter;
            buffer.Duration = builderEntry.Duration;
            buffer.GetSetState = state;
            buffer.GetCallback = Unsafe.As<Func<object?, TValue>>(getCallback);
            buffer.SetCallback = Unsafe.As<Action<object?, TValue>>(setCallback);
            return new(buffer, buffer.Version);
        }
    }

    extension<TValue, TAdapter>(TweenBuilder<TValue, TAdapter> builder)
        where TAdapter : ITweenAdapter<TValue>, IRelativeAdapter<TValue>
    {
        public TweenBuilder<TValue, TAdapter> WithRelative()
        {
            builder.Validate();
            builder.Buffer.IsRelative = true;
            return builder;
        }
    }
}

[MustUseThis("Schedule or Build")]
public readonly struct TweenBuilder<TValue, TAdapter> : IDisposable where TAdapter : ITweenAdapter<TValue>
{
    internal readonly TweenBuilderBuffer<TValue, TAdapter> Buffer;
    internal readonly ushort Version;

    internal TweenBuilder(TweenBuilderBuffer<TValue, TAdapter> buffer, ushort version)
    {
        Buffer = buffer;
        Version = version;
    }


    public void Validate()
    {
        if (Buffer.Version != Version) throw new InvalidOperationException("Tween builder Version doesn't match");
    }

    public void Run()
    {
        Schedule();
    }

    public TweenTask Schedule()
    {
        var runner = Buffer.Runner ?? ITweenRunner.Default;
        var t = Build();
        runner.Register((ITweenRunnerWorkItem)t.Promise);
        return t;
    }

    public TweenTask Build()
    {
        Validate();

        Buffer.ApplyAdapterState();
        var promise = Buffer.CreatePromise(out var token);
        return new(
            promise,
            token);
    }

    public void Dispose()
    {
        Buffer?.TryReturn();
    }
}