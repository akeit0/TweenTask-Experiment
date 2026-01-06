namespace TweenTasks;

public abstract class DeltaTimeProvider
{
    public abstract double GetCurrentTime();
    public abstract void Register(IDeltaTimeProviderWorkItem callback);
}