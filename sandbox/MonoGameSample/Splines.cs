using System;
using MathNet.Numerics.Interpolation;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace MonoGameSample;

public class Spline2D
{
    IInterpolation splineX;
    IInterpolation splineY;

    public Spline2D(ReadOnlySpan<Vector2> points)
    {
        int n = points.Length;
        double[] xs = new double[n];
        double[] ys = new double[n];
        for (int i = 0; i < n; i++)
        {
            xs[i] = (double)i / (n - 1);
            ys[i] = points[i].X;
        }

        splineX = CubicSpline.InterpolateNatural(xs, ys);
        for (int i = 0; i < n; i++)
        {
            ys[i] = points[i].Y;
        }

        splineY = CubicSpline.InterpolateNatural(xs, ys);
    }

    public Vector2 GetPoint(double t)
    {
        float x = (float)splineX.Interpolate(t);
        float y = (float)splineY.Interpolate(t);
        return new Vector2(x, y);
    }
}