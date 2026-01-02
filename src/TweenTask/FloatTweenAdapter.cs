namespace TweenTasks
{
    public readonly struct FloatTweenAdapter(float start, float end) : ITweenAdapter<float>
    {
        public readonly float Start = start;
        public readonly float End = end;

        public float Evaluate(double progress)
        {
            return (float)(Start + (End - Start) * progress);
        }

        public void Dispose()
        {
        }
    }
}