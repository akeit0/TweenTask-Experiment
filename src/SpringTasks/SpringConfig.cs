namespace SpringTasks;

public readonly record struct SpringConfig
{
    public SpringConfig(float Stiffness, float Damping, float Mass,float PositionEpsilon = 1f,float VelocityEpsilon = 0.01f)
    {
        this.Stiffness = Stiffness;
        this.Damping = Damping;
        this.Mass = Mass;
        this.PositionEpsilon = PositionEpsilon;
        this.VelocityEpsilon = VelocityEpsilon;
    }

    public float Stiffness { get; init; }
    public float Damping { get; init; }
    public float Mass { get; init; }
    public float PositionEpsilon { get; init; }
    public float VelocityEpsilon { get; init; }
}
