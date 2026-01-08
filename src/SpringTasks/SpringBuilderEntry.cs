namespace SpringTasks;

public struct SpringBuilderEntry<TValue, TAdapter>(TAdapter adapter)
    where TAdapter : ISpringAdapter<TValue>
{
    public TAdapter Adapter = adapter;
}