namespace TweenTasks;

public enum TweenResultType : byte
{
    Complete,
    Cancel
}

public readonly record struct TweenResult(TweenResultType ResultType)
{
}