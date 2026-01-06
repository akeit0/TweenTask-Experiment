using System;

namespace TweenTasks;

public enum TweenEventType : byte
{
    Start,
    LoopComplete,
    Complete,
    Cancel
}

public readonly record struct TweenEvent(TweenEventType EventType, int CompletedLoops = 0, Exception? Exception = null)
{
    public bool IsEnd => EventType is TweenEventType.Complete or TweenEventType.Cancel;
}