using static System.Math;

namespace TweenTasks
{
    public enum Ease
    {
        Linear,
        InSine,
        OutSine,
        InOutSine,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InQuart,
        OutQuart,
        InOutQuart,
        InQuint,
        OutQuint,
        InOutQuint,
        InExpo,
        OutExpo,
        InOutExpo,
        InCirc,
        OutCirc,
        InOutCirc,
        InElastic,
        OutElastic,
        InOutElastic,
        InBack,
        OutBack,
        InOutBack,
        InBounce,
        OutBounce,
        InOutBounce
    }

    public static class EaseUtility
    {
        public static double Evaluate(double t, Ease ease)
        {
            return ease switch
            {
                Ease.InSine => InSine(t),
                Ease.OutSine => OutSine(t),
                Ease.InOutSine => InOutSine(t),
                Ease.InQuad => InQuad(t),
                Ease.OutQuad => OutQuad(t),
                Ease.InOutQuad => InOutQuad(t),
                Ease.InCubic => InCubic(t),
                Ease.OutCubic => OutCubic(t),
                Ease.InOutCubic => InOutCubic(t),
                Ease.InQuart => InQuart(t),
                Ease.OutQuart => OutQuart(t),
                Ease.InOutQuart => InOutQuart(t),
                Ease.InQuint => InQuint(t),
                Ease.OutQuint => OutQuint(t),
                Ease.InOutQuint => InOutQuint(t),
                Ease.InExpo => InExpo(t),
                Ease.OutExpo => OutExpo(t),
                Ease.InOutExpo => InOutExpo(t),
                Ease.InCirc => InCirc(t),
                Ease.OutCirc => OutCirc(t),
                Ease.InOutCirc => InOutCirc(t),
                Ease.InElastic => InElastic(t),
                Ease.OutElastic => OutElastic(t),
                Ease.InOutElastic => InOutElastic(t),
                Ease.InBack => InBack(t),
                Ease.OutBack => OutBack(t),
                Ease.InOutBack => InOutBack(t),
                Ease.InBounce => InBounce(t),
                Ease.OutBounce => OutBounce(t),
                Ease.InOutBounce => InOutBounce(t),
                _ => t
            };
        }


        public static double Linear(double x)
        {
            return x;
        }


        public static double InSine(double x)
        {
            return 1 - Cos(x * PI / 2);
        }


        public static double OutSine(double x)
        {
            return Sin(x * PI / 2);
        }


        public static double InOutSine(double x)
        {
            return -(Cos(PI * x) - 1) / 2;
        }


        public static double InQuad(double x)
        {
            return x * x;
        }


        public static double OutQuad(double x)
        {
            return 1 - (1 - x) * (1 - x);
        }


        public static double InOutQuad(double x)
        {
            return x < 0.5f ? 2 * x * x : 1 - Pow(-2 * x + 2, 2) / 2;
        }


        public static double InCubic(double x)
        {
            return x * x * x;
        }


        public static double OutCubic(double x)
        {
            return 1 - Pow(1 - x, 3);
        }


        public static double InOutCubic(double x)
        {
            return x < 0.5f ? 4 * x * x * x : 1 - Pow(-2 * x + 2, 3) / 2;
        }


        public static double InQuart(double x)
        {
            return x * x * x * x;
        }


        public static double OutQuart(double x)
        {
            return 1 - Pow(1 - x, 4);
        }


        public static double InOutQuart(double x)
        {
            return x < 0.5 ? 8 * x * x * x * x : 1 - Pow(-2 * x + 2, 4) / 2;
        }


        public static double InQuint(double x)
        {
            return x * x * x * x * x;
        }


        public static double OutQuint(double x)
        {
            return 1 - Pow(1 - x, 5);
        }


        public static double InOutQuint(double x)
        {
            return x < 0.5f ? 16 * x * x * x * x * x : 1 - Pow(-2 * x + 2, 5) / 2;
        }


        public static double InExpo(double x)
        {
            return x == 0 ? 0 : Pow(2, 10 * x - 10);
        }


        public static double OutExpo(double x)
        {
            return x == 1 ? 1 : 1 - Pow(2, -10 * x);
        }


        public static double InOutExpo(double x)
        {
            return x == 0 ? 0 :
                x == 1 ? 1 :
                x < 0.5f ? Pow(2, 20 * x - 10) / 2 :
                (2 - Pow(2, -20 * x + 10)) / 2;
        }


        public static double InCirc(double x)
        {
            return 1 - Sqrt(1 - Pow(x, 2));
        }


        public static double OutCirc(double x)
        {
            return Sqrt(1 - Pow(x - 1, 2));
        }


        public static double InOutCirc(double x)
        {
            return x < 0.5 ? (1 - Sqrt(1 - Pow(2 * x, 2))) / 2 : (Sqrt(1 - Pow(-2 * x + 2, 2)) + 1) / 2;
        }


        public static double InBack(double x)
        {
            const double c1 = 1.70158f;
            const double c3 = c1 + 1;
            return c3 * x * x * x - c1 * x * x;
        }


        public static double OutBack(double x)
        {
            const double c1 = 1.70158f;
            const double c3 = c1 + 1;
            return 1 + c3 * Pow(x - 1, 3) + c1 * Pow(x - 1, 2);
        }


        public static double InOutBack(double x)
        {
            const double c1 = 1.70158f;
            const double c2 = c1 * 1.525f;

            return x < 0.5f
                ? Pow(2 * x, 2) * ((c2 + 1) * 2 * x - c2) / 2
                : (Pow(2 * x - 2, 2) * ((c2 + 1) * (x * 2 - 2) + c2) + 2) / 2;
        }


        public static double InElastic(double x)
        {
            const double c4 = 2 * PI / 3;

            return x == 0 ? 0 :
                x == 1 ? 1 :
                -Pow(2, 10 * x - 10) * Sin((x * 10 - 10.75f) * c4);
        }


        public static double OutElastic(double x)
        {
            const double c4 = 2 * PI / 3;

            return x == 0 ? 0 :
                x == 1 ? 1 :
                Pow(2, -10 * x) * Sin((x * 10 - 0.75f) * c4) + 1;
        }


        public static double InOutElastic(double x)
        {
            const double c5 = 2 * PI / 4.5f;

            return x == 0 ? 0 :
                x == 1 ? 1 :
                x < 0.5f ? -(Pow(2, 20 * x - 10) * Sin((20 * x - 11.125f) * c5)) / 2 :
                Pow(2, -20 * x + 10) * Sin((20 * x - 11.125f) * c5) / 2 + 1;
        }


        public static double InBounce(double x)
        {
            return 1 - OutBounce(1 - x);
        }


        public static double OutBounce(double x)
        {
            const double n1 = 7.5625f;
            const double d1 = 2.75f;
            var t = x;

            if (t < 1 / d1) return n1 * t * t;

            if (t < 2 / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;

            if (t < 2.5 / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;

            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }

        public static double InOutBounce(double x)
        {
            return x < 0.5f ? (1 - OutBounce(1 - 2 * x)) / 2 : (1 + OutBounce(2 * x - 1)) / 2;
        }
    }
}