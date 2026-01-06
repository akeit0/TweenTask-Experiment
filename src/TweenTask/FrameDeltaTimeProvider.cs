namespace TweenTasks;

public abstract class FrameDeltaTimeProvider
{
    public abstract long GetFrameCount();
    public abstract void Register(IFrameDeltaTimeProviderWorkItem callback,bool forceNextFrame = true);
}