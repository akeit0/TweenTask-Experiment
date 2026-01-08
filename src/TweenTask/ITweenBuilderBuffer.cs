using TweenTasks.Internal;
using MotionTasks;
namespace TweenTasks;

internal interface ITweenBuilderBuffer :IReturnable
{
    public TweenPromise CreatePromise(out short token);
    public double TotalDuration { get; }
}