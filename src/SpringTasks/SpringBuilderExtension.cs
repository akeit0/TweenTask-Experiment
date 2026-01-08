using System;
using System.Runtime.CompilerServices;
using System.Threading;
using SpringTasks.Internal;

namespace SpringTasks;

public static class SpringBuilderExtension
{
    private static readonly Action<object?, SpringEvent> OnEndWrapAction =
        (state, result) => Unsafe.As<Action<SpringEvent>>(state)(result);

    extension<TValue, TAdapter>(SpringBuilder<TValue, TAdapter> builder) where TAdapter : ISpringAdapter<TValue>
    {
        public SpringBuilder<TValue, TAdapter> WithCancellationToken(CancellationToken ct)
        {
            builder.Validate();
            builder.Buffer.CancellationToken = ct;
            return builder;
        }

        public SpringBuilder<TValue, TAdapter> WithOnEvent<TState>(TState state,
            Action<TState, SpringEvent> callback) where TState : class
        {
            builder.Validate();
            builder.Buffer.OnEndState = state;
            builder.Buffer.OnEndAction = Unsafe.As<Action<object?, SpringEvent>>(callback);
            return builder;
        }

        public SpringBuilder<TValue, TAdapter> WithOnEvent(Action<SpringEvent> callback)
        {
            builder.Validate();
            builder.Buffer.OnEndState = callback;
            builder.Buffer.OnEndAction = OnEndWrapAction;
            return builder;
        }
        
        public SpringBuilder<TValue, TAdapter> WithToGetter<TState>(TState state,Func<TState,TValue> callback) where  TState : class
        {
            builder.Validate();
            builder.Buffer.ToGetterState = state;
            builder.Buffer.ToGetter = Unsafe.As<Func<object?, TValue>>(callback);
            return builder;
        } 
    }


    extension<TValue, TOption, TAdapter>(SpringBuilder<TValue, TAdapter> builder)
        where TAdapter : ISpringAdapter<TOption, TValue>
    {
        public SpringBuilder<TValue, TAdapter> WithOption(TOption option)
        {
            builder.Validate();
            builder.Buffer.Adapter.WithOption(option);
            return builder;
        }
    }
}