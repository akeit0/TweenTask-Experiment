namespace TweenTasks;

public struct TweenToBuilderEntry<TValue, TAdapter>(TAdapter adapter, double duration)
    where TAdapter : ITweenAdapter<TValue>, ITweenFromAdapter<TValue>
{
    public TAdapter Adapter = adapter;
    public double Duration = duration;
}