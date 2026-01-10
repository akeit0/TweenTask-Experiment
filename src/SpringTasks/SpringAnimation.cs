using System;
using System.Numerics;

namespace SpringTasks
{
    public static class SpringAnimation
    {
        public static void Evaluate(
            ref float position,
            ref float velocity,
            float t,
            float to,
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
                velocity = exp * (-A * wd * sin + B * wd * cos) - zeta * w0 * y;
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
                velocity = B * exp - w0 * y;
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

        public static void Evaluate(
            float t,
            float w0,
            float zeta, out float posPos, out float posVel, out float velPos, out float velVel)
        {
            const float eps = 1e-7f;

            // アンダーダンプ
            if (zeta < 1.0 - 1e-5)
            {
                float wd = w0 * MathF.Sqrt(1.0f - zeta * zeta); // 減衰固有角周波数
                float exp = MathF.Exp(-zeta * w0 * t);
                float cos = MathF.Cos(wd * t);
                float sin = MathF.Sin(wd * t);

                // y(t) = e^{-ζω0 t} [ A cos(wd t) + B sin(wd t) ]
                // A = y0
                // y'(0) = v0 = -ζω0 A + wd B  => B = (v0 + ζω0 A) / wd
                // var A = y0;// (1,0)
                //   var B = (v0 + zeta * w0 * A) / wd; //(zeta*w0/wd,1/wd)
                //    var y = exp * (A * cos + B * sin); //(exp * (cos+w0*zeta*sin/wd), exp * sin/wd)
                var zetaW0 = zeta * w0;
                // var a = new Vector2(1, 0);
                // var b = new Vector2(zetaW0 / wd, 1 / wd);
                // var y = exp * (a * cos + b * sin);
                // var v = exp * wd * (-a * sin + b * cos) - zetaW0 * y;
                // pospos = y.X;
                // posvel = y.Y;
                // velPos = v.X;
                // velVel = v.Y;

                posPos = exp * (cos + zetaW0 * sin / wd);
                posVel = exp * sin / wd;
                velPos = exp * (-wd * sin + zetaW0 * cos) - zetaW0 * posPos;
                velVel = exp * cos - zetaW0 * posVel;
                // y'(t) = exp * { -ζω0 (A cos + B sin) + (-A wd sin + B wd cos) }
                //velocity = exp * wd * (-A * sin + B * cos) - zeta * w0 * y;
                //position = to + y;
                return;
            }

            if (1 + 1e-5f < zeta)
            {
                var s = MathF.Sqrt(zeta * zeta - 1.0f);
                var r1 = -w0 * (zeta - s);
                var r2 = -w0 * (zeta + s);
                var denom = (r1 - r2);
                if (MathF.Abs(denom) > eps)
                {
                    var e1 = MathF.Exp(r1 * t);
                    var e2 = MathF.Exp(r2 * t) / e1;
                    //var C1 = (v0 - r2 * y0) / denom; //(-r2/denom, 1/denom)
                    //var C2 = (y0 - C1); //(1 + r2/denom, -1/denom)
                    //C1 *= e1; //(-r2*e1/denom, e1/denom)
                    //C2 *= e2; //(e2 + r2*e2/denom, -e2/denom)

                    posPos = (-r2 * e1 + r2 * e2) / denom + e2;
                    posVel = (e1 - e2) / denom;
                    velPos = ((e2 * r2 - e1 * r1) / denom + e2) * r2;
                    velVel = (e1 * r1 + e2 * r2) / denom;
                    // {
                    //     var y = C1 + C2;
                    //     velocity = r1 * C1 + r2 * C2;
                    //
                    //     position = to + y;
                    // }
                }
            }

            // 臨界減衰（ζ == 1）
            {
                // y(t) = (A + B t) e^{-w0 t}
                // A = y0
                // y'(0)=v0 = B - w0 A => B = v0 + w0 A
                //var A = y0; //(1,0)
                //var B = v0 + w0 * A; //(w0,1)

                var exp = MathF.Exp(-w0 * t);
                //var y = (A + B * t) * exp; //(exp*(1+w0*t), exp*t)
                posPos = exp * (1 + w0 * t);
                posVel = exp * t;
                velPos = w0 * exp - w0 * posPos;
                velVel = exp * (1 - w0 * t);
                // y'(t) = [B - w0(A + Bt)] e^{-w0 t}
                //velocity = (B) * exp - y * w0;
                //position = to + y;
                // return;
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