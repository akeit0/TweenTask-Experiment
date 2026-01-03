namespace TweenTasks;

public struct TweenBuilderEntry<TValue, TAdapter>(TAdapter adapter, double duration)
    where TAdapter : ITweenAdapter<TValue>
{
    public TAdapter Adapter = adapter;
    public double Duration = duration;
}