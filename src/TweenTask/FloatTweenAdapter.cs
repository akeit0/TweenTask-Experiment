namespace TweenTasks;

public struct FloatTweenAdapter(float from, float to)
    : ITweenFromAdapter<float>, IRelativeAdapter<float>
{
    public float To = to;
    public float From = from;
    public FloatTweenAdapter(float to) : this(default, to){}

    float ITweenAdapter<float>.From => From;

    public void ApplyFrom(float from, bool isRelative)
    {
        From = from;
        if (isRelative)
        {
            To += from;
        }
    }

    public float Evaluate(double progress)
    {
        return (float)(From + (To - From) * progress);
    }
}

public struct DoubleTweenAdapter(double from, double to)
    : ITweenFromAdapter<double>, IRelativeAdapter<float>
{
    public double To = to;
    public double From = from;

    double ITweenAdapter<double>.From => From;

    public void ApplyFrom(double from, bool isRelative)
    {
        From = from;
        if (isRelative)
        {
            To += from;
        }
    }

    public double Evaluate(double progress)
    {
        return (From + (To - From) * progress);
    }
}