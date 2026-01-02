using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TweenTasks.Internal;

namespace TweenTasks
{
    public readonly struct TweenTask : IEquatable<TweenTask>
    {
        public static TweenBuilder<T, TAdapter> CreateFromAdapter<T, TAdapter>(TAdapter adapter, double duration)
            where TAdapter : ITweenAdapter<T>
        {
            return TweenBuilder<T, TAdapter>.Create(adapter, duration);
        }

        public static TweenBuilder<float, FloatTweenAdapter> Create(float start, float end, double duration)
        {
            return TweenBuilder<float, FloatTweenAdapter>.Create(new(start, end), duration);
        }

        private readonly TweenPromise promise;
        private readonly short token;

        internal TweenTask(TweenPromise promise, short token)
        {
            this.promise = promise;
            this.token = token;
        }

        public void SetPlaybackSpeed(double speed)
        {
            promise.PlaybackSpeed = speed;
        }

        public bool TryCancel()
        {
            return promise.TryCancel(token);
        }

        public bool TryComplete()
        {
            return promise.TryComplete(token);
        }

        public ValueTask AsValueTask()
        {
            return new(promise, token);
        }

        public void Forget()
        {
        }

        public ValueTaskAwaiter GetAwaiter()
        {
            return new ValueTask(promise, token).GetAwaiter();
        }

        public bool Equals(TweenTask other)
        {
            return promise == other.promise && token == other.token;
        }

        public override bool Equals(object? obj)
        {
            return obj is TweenTask other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(promise, token);
        }
    }
}