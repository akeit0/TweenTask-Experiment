using System;

namespace TweenTasks
{
    public interface ITweenAdapter<in TOption, out T> : ITweenAdapter<T>
    {
        void WithOption(TOption option)
        {
        }
    }

    public struct NoOption
    {
    }

    public interface ITweenAdapter<out T> : IDisposable
    {
        T Evaluate(double progress);
    }
}