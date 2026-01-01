namespace TweenTasks
{
    public abstract class TweenRunner
    {
        public abstract double GetCurrentTime();
        public abstract void Register(ITweenRunnerWorkItem callback);
    }
}