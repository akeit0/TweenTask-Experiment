namespace TweenTasks;

public interface IDeltaTimeProviderWorkItem
{
    // true, continue
    bool MoveNext(double deltaTime);
}