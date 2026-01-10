using System;
using System.Runtime.CompilerServices;
using System.Threading;
using MotionTasks;
using TweenTasks.Internal;

namespace TweenTasks;

public static class TweenBuilderExtension
{
    private static readonly Action<object?, TweenEvent> OnEndWrapAction =
        (state, result) => Unsafe.As<Action<TweenEvent>>(state)(result);

    extension<TValue, TAdapter>(TweenBuilder<TValue, TAdapter> builder) where TAdapter : ITweenAdapter<TValue>
    {
        public TweenBuilder<TValue, TAdapter> WithCancellationToken(CancellationToken ct)
        {
            builder.Validate();
            builder.Buffer.CancellationToken = ct;
            return builder;
        }

        public TweenBuilder<TValue, TAdapter> WithLoop(int loopCount, LoopType loopType = LoopType.Restart)
        {
            builder.Validate();
            builder.Buffer.LoopCount = loopCount;
            builder.Buffer.LoopType = loopType;
            return builder;
        }


        public TweenBuilder<TValue, TAdapter> WithDelay(double delay, DelayType delayType = DelayType.FirstLoop,
            bool skipValuesDuringDelay = true)
        {
            builder.Validate();
            var buffer = builder.Buffer;
            buffer.Delay = delay;
            if (skipValuesDuringDelay)
            {
                buffer.Flags.ClearFlags(TweenTaskSettingFlags.ApplyValuesDuringDelay);
            }
            else
            {
                buffer.Flags.SetFlags(TweenTaskSettingFlags.ApplyValuesDuringDelay);
            }

            if (delayType == DelayType.FirstLoop)
            {
                buffer.Flags.ClearFlags(TweenTaskSettingFlags.DelayEveryLoop);
            }
            else
            {
                buffer.Flags.SetFlags(TweenTaskSettingFlags.DelayEveryLoop);
            }

            return builder;
        }

        public TweenBuilder<TValue, TAdapter> WithOnEvent<TState>(TState state,
            Action<TState, TweenEvent> callback) where TState : class
        {
            builder.Validate();
            builder.Buffer.OnEndState = state;
            builder.Buffer.OnEndAction = Unsafe.As<Action<object?, TweenEvent>>(callback);
            return builder;
        }

        public TweenBuilder<TValue, TAdapter> WithOnEvent(Action<TweenEvent> callback)
        {
            builder.Validate();
            builder.Buffer.OnEndState = callback;
            builder.Buffer.OnEndAction = OnEndWrapAction;
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
    }

    extension(TweenSequenceBuilder builder)
    {
        public TweenSequenceBuilder WithOnEvent<TState>(TState state,
            Action<TState, TweenEvent> callback) where TState : class
        {
            builder.Validate();
            builder.buffer.EndState = state;
            builder.buffer.OnEndAction = Unsafe.As<Action<object?, TweenEvent>>(callback);
            return builder;
        }

        public TweenSequenceBuilder WithLoop(int loopCount, LoopType loopType = LoopType.Restart)
        {
            builder.Validate();
            builder.buffer.LoopCount = loopCount;
            builder.buffer.LoopType = loopType;
            return builder;
        }
    }

    extension<TValue, TOption, TAdapter>(TweenBuilder<TValue, TAdapter> builder)
        where TAdapter : ITweenAdapter<TOption, TValue>
    {
        public TweenBuilder<TValue, TAdapter> WithOption(TOption option)
        {
            builder.Validate();
            builder.Buffer.Adapter.WithOption(option);
            return builder;
        }
    }
}