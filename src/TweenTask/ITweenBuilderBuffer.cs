using TweenTasks.Internal;

namespace TweenTasks;

internal interface ITweenBuilderBuffer :IReturnable
{
    public TweenPromise CreatePromise(out short token);
    public double TotalDuration { get; }
}