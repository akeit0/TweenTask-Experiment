using System;
using System.Runtime.CompilerServices;
using System.Threading;
using SpringTasks.Internal;
using MotionTasks;
namespace SpringTasks;

public static class SpringBuilder
{
    public static SpringBuilderEntry<TValue, TAdapter> CreateEntry<TValue, TAdapter>(TAdapter adapter)
        where TAdapter : ISpringAdapter<TValue>, ISpringFromAdapter<TValue>
    {
        return new SpringBuilderEntry<TValue, TAdapter>(adapter);
    }

    public static SpringToBuilderEntry<TValue, TAdapter> CreateToEntry<TValue, TAdapter>(TAdapter adapter)
        where TAdapter : ISpringAdapter<TValue>, ISpringFromAdapter<TValue>
    {
        return new SpringToBuilderEntry<TValue, TAdapter>(adapter);
    }

    extension<TValue, TAdapter>(SpringBuilderEntry<TValue, TAdapter> builderEntry) where TAdapter : ISpringAdapter<TValue>
    {
        public SpringBuilder<TValue, TAdapter> Bind<TState>(TState state, Action<TState, TValue> callback)
            where TState : class
        {
            var buffer = SpringBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.Adapter = builderEntry.Adapter;
            buffer.GetSetState = state;
            buffer.SetCallback = Unsafe.As<Action<object?, TValue>>(callback);
            return new(buffer, buffer.Version);
        }

        public SpringBuilder<TValue, TAdapter> Bind<TState>(TState state, Action<TState, TValue> callback,
            CancellationToken cancellationToken) where TState : class
        {
            var buffer = SpringBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.CancellationToken = cancellationToken;
            buffer.Adapter = builderEntry.Adapter;
            buffer.GetSetState = state;
            buffer.SetCallback = Unsafe.As<Action<object?, TValue>>(callback);
            return new(buffer, buffer.Version);
        }

        public SpringBuilder<TValue, TAdapter> Bind(Action<TValue> callback)
        {
            var buffer = SpringBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.Adapter = builderEntry.Adapter;
            buffer.GetSetState = callback;
            buffer.SetCallback = static (o, value) => { Unsafe.As<Action<TValue>>(o)(value); };
            return new(buffer, buffer.Version);
        }

        public SpringBuilder<TValue, TAdapter> Bind(Action<TValue> callback, CancellationToken cancellationToken)
        {
            var buffer = SpringBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.CancellationToken = cancellationToken;
            buffer.Adapter = builderEntry.Adapter;
            buffer.GetSetState = callback;
            buffer.SetCallback = static (o, value) => { Unsafe.As<Action<TValue>>(o)(value); };
            return new(buffer, buffer.Version);
        }
    }

    extension<TValue, TAdapter>(SpringToBuilderEntry<TValue, TAdapter> builderEntry)
        where TAdapter : ISpringAdapter<TValue>, ISpringFromAdapter<TValue>
    {
        public SpringBuilder<TValue, TAdapter> Bind<TState>(TState state, Func<TState, TValue> getCallback,
            Action<TState, TValue> setCallback) where TState : class
        {
            var buffer = SpringBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.Adapter = builderEntry.Adapter;
            buffer.GetSetState = state;
            buffer.GetCallback = Unsafe.As<Func<object?, TValue>>(getCallback);
            buffer.SetCallback = Unsafe.As<Action<object?, TValue>>(setCallback);
            return new(buffer, buffer.Version);
        }

        public SpringBuilder<TValue, TAdapter> Bind<TState>(TState state, Func<TState, TValue> getCallback,
            Action<TState, TValue> setCallback, CancellationToken cancellationToken) where TState : class
        {
            var buffer = SpringBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.CancellationToken = cancellationToken;
            buffer.Adapter = builderEntry.Adapter;
            buffer.GetSetState = state;
            buffer.GetCallback = Unsafe.As<Func<object?, TValue>>(getCallback);
            buffer.SetCallback = Unsafe.As<Action<object?, TValue>>(setCallback);
            return new(buffer, buffer.Version);
        }
    }

    extension<TValue, TAdapter>(SpringBuilder<TValue, TAdapter> builder)
        where TAdapter : ISpringAdapter<TValue>, IRelativeAdapter<TValue>
    {
        public SpringBuilder<TValue, TAdapter> WithRelative()
        {
            builder.Validate();
            builder.Buffer.Flags.SetFlags(SpringTaskSettingFlags.IsRelative);
            return builder;
        }
    }
}

[MustUseThis("Schedule or Build")]
public readonly struct SpringBuilder<TValue, TAdapter> : IDisposable where TAdapter : ISpringAdapter<TValue>
{
    internal readonly SpringBuilderBuffer<TValue, TAdapter> Buffer;
    internal readonly ushort Version;

    internal SpringBuilder(SpringBuilderBuffer<TValue, TAdapter> buffer, ushort version)
    {
        Buffer = buffer;
        Version = version;
    }


    public void Validate()
    {
        if (Buffer.Version != Version) throw new InvalidOperationException("Spring builder Version doesn't match");
    }

    public void Run()
    {
        Schedule();
    }

    public SpringTask Schedule(FrameDeltaTimeProvider? provider = null)
    {
        provider ??= MotionSystem.DefaultFrameDeltaTimeProvider;
        var t = Build();
        provider.Register((IFrameDeltaTimeProviderWorkItem)t.Promise, true);
        return t;
    }

    public SpringTask Build()
    {
        Validate();

        //Buffer.ApplyAdapterState();
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