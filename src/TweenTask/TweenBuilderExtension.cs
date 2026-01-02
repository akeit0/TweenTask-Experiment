using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TweenTasks
{
    public static class TweenBuilderExtension
    {
        extension<TValue, TAdapter>(TweenBuilder<TValue, TAdapter> builder) where TAdapter : ITweenAdapter<TValue>
        {
            public TweenBuilder<TValue, TAdapter> WithCancellationToken(CancellationToken ct)
            {
                builder.Validate();
                builder.Buffer.CancellationToken = ct;
                return builder;
            }

            public TweenBuilder<TValue, TAdapter> WithRunner(ITweenRunner runner)
            {
                builder.Validate();
                builder.Buffer.Runner = runner;
                return builder;
            }

            public TweenBuilder<TValue, TAdapter> WithEase(Ease ease)
            {
                builder.Validate();
                builder.Buffer.Ease = ease;
                return builder;
            }

            public TweenBuilder<TValue, TAdapter> WithPlaybackSpeed(double speed)
            {
                builder.Validate();
                builder.Buffer.PlaybackSpeed = speed;
                return builder;
            }

            public TweenTask Bind<TState>(TState state, Action<TState, TValue> callback) where TState : class
            {
                return builder.Bind(Unsafe.As<Action<object?, TValue>>(callback), state);
            }

            public TweenTask Bind<TState>(TState state, Action<TState, TValue> callback,
                CancellationToken cancellationToken) where TState : class
            {
                return builder.Bind(state, Unsafe.As<Action<object?, TValue>>(callback), cancellationToken);
            }
        }

        extension<TValue, TOption, TAdapter>(TweenBuilder<TValue, TAdapter> builder)
            where TAdapter : ITweenAdapter<TOption, TValue>
        {
            public TweenBuilder<TValue, TAdapter> WithOption(TOption option)
            {
                builder.Validate();
                builder.Buffer.Adapter.WithOption(option);
                ;
                return builder;
            }
        }
    }
}