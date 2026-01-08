namespace SpringTasks;

public readonly record struct SpringEvent(SpringEventType EventType, int CompletedLoops = 0, int LoopCount = 0)
{
    public bool IsEnd => EventType is SpringEventType.Complete or SpringEventType.Cancel;
}