namespace SpringTasks;

public struct SpringToBuilderEntry<TValue, TAdapter>(TAdapter adapter)
    where TAdapter : ISpringAdapter<TValue>, ISpringFromAdapter<TValue>
{
    public TAdapter Adapter = adapter;
}