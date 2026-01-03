namespace TweenTasks;

public interface ITweenRunner
{
    public static ITweenRunner Default { get; set; }
    public double GetCurrentTime();
    public void Register(ITweenRunnerWorkItem callback);
}