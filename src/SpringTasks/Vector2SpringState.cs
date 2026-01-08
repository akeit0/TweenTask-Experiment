using System.Numerics;

namespace SpringTasks;

public struct Vector2SpringState
{
    public Vector2 Position;
    public Vector2 Velocity;

    public Vector2SpringState(Vector2 position, Vector2 velocity)
    {
        Position = position;
        Velocity = velocity;
    }

    public override string ToString() => $"x={Position}, v={Velocity}";
}