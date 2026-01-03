namespace TweenTasks;

public interface ITaskPoolNode<T>
{
    ref T? NextNode { get; }
}