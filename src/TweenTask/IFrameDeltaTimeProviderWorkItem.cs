namespace TweenTasks;

public interface IFrameDeltaTimeProviderWorkItem
{
    // true, continue
    bool MoveNext(FrameInfo info);
}

public readonly struct FrameInfo(long frameCount, double deltaTime)
{
    public long FrameCount { get; } = frameCount;
    public double DeltaTime { get; } = deltaTime;
}