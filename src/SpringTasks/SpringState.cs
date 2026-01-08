namespace SpringTasks;

public struct SpringState
{
    public double Position;
    public double Velocity;

    public SpringState(double position, double velocity)
    {
        Position = position;
        Velocity = velocity;
    }

    public override string ToString() => $"x={Position}, v={Velocity}";
}