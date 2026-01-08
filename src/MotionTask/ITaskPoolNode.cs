namespace MotionTasks;

public interface ITaskPoolNode<T>
{
    ref T? NextNode { get; }
}