namespace TweenTasks;

public enum TweenEventType : byte
{
    Start,
    LoopComplete,
    Complete,
    Cancel
}

public readonly record struct TweenEvent(TweenEventType EventType, int CompletedLoops)
{
    public TweenEvent(TweenEventType eventType) : this(eventType, 0)
    {
    }
}