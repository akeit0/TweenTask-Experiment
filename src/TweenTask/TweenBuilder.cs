using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TweenTasks.Internal;

namespace TweenTasks
{
    public readonly struct TweenBuilder<TValue, TAdapter> : IDisposable where TAdapter : ITweenAdapter<TValue>
    {
        internal readonly TweenBuilderBuffer<TValue, TAdapter> Buffer;
        internal readonly ushort Version;

        internal TweenBuilder(TweenBuilderBuffer<TValue, TAdapter> buffer, ushort version)
        {
            Buffer = buffer;
            Version = version;
        }

        public static TweenBuilder<TValue, TAdapter> Create(TAdapter adapter, double duration)
        {
            var buffer = TweenBuilderBuffer<TValue, TAdapter>.Rent();
            buffer.Adapter = adapter;
            buffer.Duration = duration;
            return new(buffer, buffer.Version);
        }

        public void Validate()
        {
            if (Buffer.Version != Version) throw new InvalidOperationException("Tween builder Version doesn't match");
        }

        public TweenTask Bind(Action<object?, TValue> callback, object? state)
        {
            Validate();

            var promise = TweenPromise<TValue, TAdapter>.Create(Buffer.Delay,
                Buffer.Duration, Buffer.PlaybackSpeed, Buffer.Ease, Buffer.Adapter, callback, state,
                Buffer.CancellationToken,
                out var token);
            (Buffer.Runner ?? ITweenRunner.Default).Register(promise);
            return new(
                promise,
                token);
        }

        public TweenTask Bind(object? state, Action<object?, TValue> callback, CancellationToken cancellationToken)
        {
            Validate();
            var promise = TweenPromise<TValue, TAdapter>.Create(Buffer.Delay,
                Buffer.Duration, Buffer.PlaybackSpeed, Buffer.Ease, Buffer.Adapter, callback, state, cancellationToken,
                out var token);
            (Buffer.Runner ?? ITweenRunner.Default).Register(promise);
            return new(
                promise,
                token);
        }

        public TweenTask Bind(Action<TValue> callback, CancellationToken cancellationToken)
        {
            Validate();
            var promise = TweenPromise<TValue, TAdapter>.Create(Buffer.Delay,
                Buffer.Duration, Buffer.PlaybackSpeed, Buffer.Ease, Buffer.Adapter,
                (state, value) => Unsafe.As<Action<TValue>>(state)(value), callback, cancellationToken,
                out var token);
            (Buffer.Runner ?? ITweenRunner.Default).Register(promise);
            return new(
                promise,
                token);
        }

        public void Dispose()
        {
            Buffer?.Return();
        }
    }
}