using System;

namespace SpringTasks;

public record struct SpringConfig
{
    public SpringConfig(float stiffness, float damping, float mass,float positionEpsilon ,float velocityEpsilon)
    {
        Frequency = MathF.Sqrt(stiffness / mass); // 自然角周波数
        DumpingRatio = damping / (2.0f * MathF.Sqrt(stiffness * mass)); // 減衰比
        this.PositionEpsilon = positionEpsilon;
        this.VelocityEpsilon = velocityEpsilon;
    }
    
    public SpringConfig(float stiffness, float damping, float mass)
    {
        Frequency = MathF.Sqrt(stiffness / mass); // 自然角周波数
        DumpingRatio = damping / (2.0f * MathF.Sqrt(stiffness * mass)); // 減衰比
    }
    
    public SpringConfig(float frequency, float dampingRatio)
    {
        Frequency = frequency;
        DumpingRatio = dampingRatio; // 減衰比
    }

    public float Frequency { get; set; }
    public float DumpingRatio { get; set; }
    public required float PositionEpsilon { get; set; }
    public required float VelocityEpsilon { get; set; }
}
