using System;
using System.Numerics;

namespace SpringTasks
{
    public static class SpringAnimation
    {
        public static void Evaluate(ref Vector2 position, ref Vector2 velocity, double t, Vector2 to,
            SpringConfig config)
        {
            var resultX = Evaluate(
                t,
                position.X,
                to.X,
                velocity.X,
                config.Mass,
                config.Stiffness,
                config.Damping);
            var resultY = Evaluate(
                t,
                position.Y,
                to.Y,
                velocity.Y,
                config.Mass,
                config.Stiffness,
                config.Damping);
            position = new Vector2((float)resultX.Position, (float)resultY.Position);
            velocity = new Vector2((float)resultX.Velocity, (float)resultY.Velocity);
        }

        public static void Evaluate(ref Vector2SpringState state, double t, Vector2 to, SpringConfig config)
        {
            var resultX = Evaluate(
                t,
                state.Position.X,
                to.X,
                state.Velocity.X,
                config.Mass,
                config.Stiffness,
                config.Damping);
            var resultY = Evaluate(
                t,
                state.Position.Y,
                to.Y,
                state.Velocity.Y,
                config.Mass,
                config.Stiffness,
                config.Damping);
            state = new Vector2SpringState(
                new Vector2((float)resultX.Position, (float)resultY.Position),
                new Vector2((float)resultX.Velocity, (float)resultY.Velocity));
        }

        public static void Evaluate(ref SpringState state, double t, double to, SpringConfig config)
        {
            state = Evaluate(
                t,
                state.Position,
                to,
                state.Velocity,
                config.Mass,
                config.Stiffness,
                config.Damping);
        }

        /// <summary>
        /// Mass-Spring-Damper の解析解で、時刻 t の位置と速度を返す。
        /// 方程式: m x'' + c x' + k (x - target) = 0
        /// </summary>
        /// <param name="t">時刻 [s]</param>
        /// <param name="from">初期位置</param>
        /// <param name="to">目標位置(target)</param>
        /// <param name="initialVelocity">初期速度 [units/s]（Apple の initialVelocity と同じく「距離/秒」想定）</param>
        /// <param name="mass">質量 m</param>
        /// <param name="stiffness">ばね定数 k</param>
        /// <param name="damping">減衰係数 c</param>
        public static SpringState Evaluate(
            double t,
            double from,
            double to,
            double initialVelocity,
            double mass,
            double stiffness,
            double damping)
        {
            if (mass <= 0) throw new ArgumentOutOfRangeException(nameof(mass), "mass must be > 0.");
            if (stiffness <= 0) throw new ArgumentOutOfRangeException(nameof(stiffness), "stiffness must be > 0.");
            if (t < 0) t = 0;

            // y = x - to（target からの偏差）に変換すると:
            // m y'' + c y' + k y = 0
            double y0 = from - to;
            double v0 = initialVelocity;

            double w0 = Math.Sqrt(stiffness / mass); // 自然角周波数
            double zeta = damping / (2.0 * Math.Sqrt(stiffness * mass)); // 減衰比

            const double eps = 1e-12;

            // アンダーダンプ
            if (zeta < 1.0 - 1e-9)
            {
                double wd = w0 * Math.Sqrt(1.0 - zeta * zeta); // 減衰固有角周波数
                double exp = Math.Exp(-zeta * w0 * t);

                // y(t) = e^{-ζω0 t} [ A cos(wd t) + B sin(wd t) ]
                // A = y0
                // y'(0) = v0 = -ζω0 A + wd B  => B = (v0 + ζω0 A) / wd
                double A = y0;
                double B = (v0 + zeta * w0 * A) / wd;

                double cos = Math.Cos(wd * t);
                double sin = Math.Sin(wd * t);

                double y = exp * (A * cos + B * sin);

                // y'(t) = exp * { -ζω0 (A cos + B sin) + (-A wd sin + B wd cos) }
                double yDot = exp * (
                    -zeta * w0 * (A * cos + B * sin)
                    + (-A * wd * sin + B * wd * cos)
                );

                return new SpringState(position: to + y, velocity: yDot);
            }
            // 臨界減衰（ζ == 1）
            else if (Math.Abs(zeta - 1.0) <= 1e-9)
            {
                // y(t) = (A + B t) e^{-w0 t}
                // A = y0
                // y'(0)=v0 = B - w0 A => B = v0 + w0 A
                double A = y0;
                double B = v0 + w0 * A;

                double exp = Math.Exp(-w0 * t);
                double y = (A + B * t) * exp;

                // y'(t) = [B - w0(A + Bt)] e^{-w0 t}
                double yDot = (B - w0 * (A + B * t)) * exp;

                return new SpringState(position: to + y, velocity: yDot);
            }
            // 過減衰（ζ > 1）
            else
            {
                // y(t) = C1 e^{r1 t} + C2 e^{r2 t}
                // r1,2 = -w0(ζ ∓ sqrt(ζ^2 - 1))
                double s = Math.Sqrt(zeta * zeta - 1.0);
                double r1 = -w0 * (zeta - s);
                double r2 = -w0 * (zeta + s);

                // 初期条件:
                // y0 = C1 + C2
                // v0 = r1 C1 + r2 C2
                // => C1 = (v0 - r2 y0)/(r1 - r2), C2 = y0 - C1
                double denom = (r1 - r2);
                if (Math.Abs(denom) < eps)
                {
                    // 数値的にはほぼ臨界なので臨界減衰として扱う
                    double A = y0;
                    double B = v0 + w0 * A;
                    double exp = Math.Exp(-w0 * t);
                    double y = (A + B * t) * exp;
                    double yDot = (B - w0 * (A + B * t)) * exp;
                    return new SpringState(position: to + y, velocity: yDot);
                }

                double C1 = (v0 - r2 * y0) / denom;
                double C2 = y0 - C1;

                double e1 = Math.Exp(r1 * t);
                double e2 = Math.Exp(r2 * t);
                {
                    double y = C1 * e1 + C2 * e2;
                    double yDot = r1 * C1 * e1 + r2 * C2 * e2;

                    return new SpringState(position: to + y, velocity: yDot);
                }
            }
        }

        public static SpringState Evaluate(
            double t,
            double from,
            double to,
            double initialVelocity,
            double w0,
            double zeta)
        {
            if (t < 0) t = 0;

            // y = x - to（target からの偏差）に変換すると:
            // m y'' + c y' + k y = 0
            double y0 = from - to;
            double v0 = initialVelocity;


            const double eps = 1e-12;

            // アンダーダンプ
            if (zeta < 1.0 - 1e-9)
            {
                double wd = w0 * Math.Sqrt(1.0 - zeta * zeta); // 減衰固有角周波数
                double exp = Math.Exp(-zeta * w0 * t);

                // y(t) = e^{-ζω0 t} [ A cos(wd t) + B sin(wd t) ]
                // A = y0
                // y'(0) = v0 = -ζω0 A + wd B  => B = (v0 + ζω0 A) / wd
                double A = y0;
                double B = (v0 + zeta * w0 * A) / wd;

                double cos = Math.Cos(wd * t);
                double sin = Math.Sin(wd * t);

                double y = exp * (A * cos + B * sin);

                // y'(t) = exp * { -ζω0 (A cos + B sin) + (-A wd sin + B wd cos) }
                double yDot = exp * (
                    -zeta * w0 * (A * cos + B * sin)
                    + (-A * wd * sin + B * wd * cos)
                );

                return new SpringState(position: to + y, velocity: yDot);
            }
            // 臨界減衰（ζ == 1）
            else if (Math.Abs(zeta - 1.0) <= 1e-9)
            {
                // y(t) = (A + B t) e^{-w0 t}
                // A = y0
                // y'(0)=v0 = B - w0 A => B = v0 + w0 A
                double A = y0;
                double B = v0 + w0 * A;

                double exp = Math.Exp(-w0 * t);
                double y = (A + B * t) * exp;

                // y'(t) = [B - w0(A + Bt)] e^{-w0 t}
                double yDot = (B - w0 * (A + B * t)) * exp;

                return new SpringState(position: to + y, velocity: yDot);
            }
            // 過減衰（ζ > 1）
            else
            {
                // y(t) = C1 e^{r1 t} + C2 e^{r2 t}
                // r1,2 = -w0(ζ ∓ sqrt(ζ^2 - 1))
                double s = Math.Sqrt(zeta * zeta - 1.0);
                double r1 = -w0 * (zeta - s);
                double r2 = -w0 * (zeta + s);

                // 初期条件:
                // y0 = C1 + C2
                // v0 = r1 C1 + r2 C2
                // => C1 = (v0 - r2 y0)/(r1 - r2), C2 = y0 - C1
                double denom = (r1 - r2);
                if (Math.Abs(denom) < eps)
                {
                    // 数値的にはほぼ臨界なので臨界減衰として扱う
                    double A = y0;
                    double B = v0 + w0 * A;
                    double exp = Math.Exp(-w0 * t);
                    double y = (A + B * t) * exp;
                    double yDot = (B - w0 * (A + B * t)) * exp;
                    return new SpringState(position: to + y, velocity: yDot);
                }

                double C1 = (v0 - r2 * y0) / denom;
                double C2 = y0 - C1;

                double e1 = Math.Exp(r1 * t);
                double e2 = Math.Exp(r2 * t);
                {
                    double y = C1 * e1 + C2 * e2;
                    double yDot = r1 * C1 * e1 + r2 * C2 * e2;

                    return new SpringState(position: to + y, velocity: yDot);
                }
            }
        }

        public static void Evaluate(
            ref Vector2 position,
            ref Vector2 velocity,
            float t,
            Vector2 to,
            float w0,
            float zeta)
        {
            if (t < 0) t = 0;

            // y = x - to（target からの偏差）に変換すると:
            // m y'' + c y' + k y = 0
            var y0 = position - to;
            var v0 = velocity;


            const float eps = 1e-12f;

            // アンダーダンプ
            if (zeta < 1.0 - 1e-9)
            {
                float wd = w0 * MathF.Sqrt(1.0f - zeta * zeta); // 減衰固有角周波数
                float exp = MathF.Exp(-zeta * w0 * t);

                // y(t) = e^{-ζω0 t} [ A cos(wd t) + B sin(wd t) ]
                // A = y0
                // y'(0) = v0 = -ζω0 A + wd B  => B = (v0 + ζω0 A) / wd
                var A = y0;
                var B = (v0 + zeta * w0 * A) / wd;

                float cos = MathF.Cos(wd * t);
                float sin = MathF.Sin(wd * t);

                var y = exp * (A * cos + B * sin);

                // y'(t) = exp * { -ζω0 (A cos + B sin) + (-A wd sin + B wd cos) }
                velocity = exp * (
                    -zeta * w0 * (A * cos + B * sin)
                    + (-A * wd * sin + B * wd * cos)
                );
                position = to + y;
                return;
            }
            // 臨界減衰（ζ == 1）
            else if (MathF.Abs(zeta - 1.0f) <= 1e-9)
            {
                // y(t) = (A + B t) e^{-w0 t}
                // A = y0
                // y'(0)=v0 = B - w0 A => B = v0 + w0 A
                var A = y0;
                var B = v0 + w0 * A;

                var exp = MathF.Exp(-w0 * t);
                var y = (A + B * t) * exp;

                // y'(t) = [B - w0(A + Bt)] e^{-w0 t}
                velocity = (B - w0 * (A + B * t)) * exp;
                position = to + y;
                return;
            }
            // 過減衰（ζ > 1）
            else
            {
                // y(t) = C1 e^{r1 t} + C2 e^{r2 t}
                // r1,2 = -w0(ζ ∓ sqrt(ζ^2 - 1))
                var s = MathF.Sqrt(zeta * zeta - 1.0f);
                var r1 = -w0 * (zeta - s);
                var r2 = -w0 * (zeta + s);

                // 初期条件:
                // y0 = C1 + C2
                // v0 = r1 C1 + r2 C2
                // => C1 = (v0 - r2 y0)/(r1 - r2), C2 = y0 - C1
                var denom = (r1 - r2);
                if (MathF.Abs(denom) < eps)
                {
                    // 数値的にはほぼ臨界なので臨界減衰として扱う
                    var A = y0;
                    var B = v0 + w0 * A;
                    var exp = MathF.Exp(-w0 * t);
                    var y = (A + B * t) * exp;
                    velocity = (B - w0 * (A + B * t)) * exp;
                    position = to + y;
                    return;
                }

                var C1 = (v0 - r2 * y0) / denom;
                var C2 = y0 - C1;

                var e1 = MathF.Exp(r1 * t);
                var e2 = MathF.Exp(r2 * t);
                {
                    var y = C1 * e1 + C2 * e2;
                    velocity = r1 * C1 * e1 + r2 * C2 * e2;

                    position = to + y;
                }
            }
        }

        /// <summary>
        /// 「十分に収束した」とみなすまでの時間の目安（厳密ではなく実務向けの近似）。
        /// Apple の "settlingDuration" 的に使う用途を想定。
        /// </summary>
        public static double ApproxSettlingDuration(
            double mass,
            double stiffness,
            double damping,
            double epsilon = 1e-3)
        {
            if (mass <= 0) throw new ArgumentOutOfRangeException(nameof(mass));
            if (stiffness <= 0) throw new ArgumentOutOfRangeException(nameof(stiffness));
            if (epsilon <= 0 || epsilon >= 1) throw new ArgumentOutOfRangeException(nameof(epsilon));

            double w0 = Math.Sqrt(stiffness / mass);
            double zeta = damping / (2.0 * Math.Sqrt(stiffness * mass));

            // envelope が epsilon まで落ちる時刻を雑に見積もる
            // アンダーダンプ: exp(-ζω0 t) <= epsilon  => t >= -ln(eps)/(ζω0)
            // 臨界/過減衰: 最も遅い極（絶対値が小さい負の実根）で見積もる
            if (zeta < 1.0 - 1e-9)
            {
                return -Math.Log(epsilon) / (zeta * w0);
            }
            else if (Math.Abs(zeta - 1.0) <= 1e-9)
            {
                // exp(-w0 t) と t の項があるが、実務的に少し余裕を見て
                return -Math.Log(epsilon) / w0;
            }
            else
            {
                double s = Math.Sqrt(zeta * zeta - 1.0);
                double rSlow = -w0 * (zeta - s); // 0 に近い方（遅い）
                return -Math.Log(epsilon) / (-rSlow);
            }
        }
    }
}