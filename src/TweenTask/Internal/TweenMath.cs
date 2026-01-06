using System;

namespace TweenTasks.Internal;

internal static class TweenMath
{
    public static double CalculateProgress(double progress, int loopCount,
        LoopType loopType, Ease ease)
    {
        var offset = 0.0;
        var factor = 1.0;
        {
            if (loopCount % 2 == 1 && loopType is LoopType.Yoyo or LoopType.Flip)
            {
                if (loopType == LoopType.Flip)
                {
                    offset = 1;
                    factor = -1;
                }
                else
                {
                    progress = 1 - progress;
                }
            }
            else
            {
                if (loopType == LoopType.Incremental)
                {
                    offset = loopCount * 1; //EaseUtility.Evaluate(1, ease);
                }
            }
        }

        return offset + factor * EaseUtility.Evaluate(Math.Clamp(progress, 0, 1), ease);
    }

    public static double CalculateProgressForLoops(double progress,
        LoopType loopType, Ease ease)
    {
        var offset = 0.0;
        var factor = 1.0;
        {
            var currentLoop = (int)(progress);
            var loopProgress = progress - currentLoop;
            if (currentLoop % 2 == 1 && loopType is LoopType.Yoyo or LoopType.Flip)
            {
                if (loopType == LoopType.Flip)
                {
                    offset = 1;
                    factor = -1;
                    progress = loopProgress;
                }
                else
                {
                    progress = 1 - loopProgress;
                }
            }
            else
            {
                progress = loopProgress;
                if (loopType == LoopType.Incremental)
                {
                    offset = currentLoop * 1; //EaseUtility.Evaluate(1, ease);
                }
            }
        }

        return offset + factor * EaseUtility.Evaluate(Math.Clamp(progress, 0, 1), ease);
    }

    // public static double CalculateProgress(double time, double duration, double delay, bool delayEveryLoop,
    //     int loopCount,
    //     LoopType loopType, Ease ease)
    // {
    //     double progress;
    //     if (delayEveryLoop)
    //     {
    //         var perLoopDuration = duration + delay;
    //         var currentLoop = (int)(time / perLoopDuration);
    //     }
    //
    //     var offset = 0.0;
    //     var factor = 1.0;
    //     if (loopCount > 1 && progress >= 1)
    //     {
    //         var currentLoop = (int)(progress);
    //         var loopProgress = progress - currentLoop;
    //         if (currentLoop % 2 == 1 && loopType is LoopType.Yoyo or LoopType.Flip)
    //         {
    //             if (loopType == LoopType.Flip)
    //             {
    //                 offset = 1;
    //                 factor = -1;
    //                 progress = loopProgress;
    //             }
    //             else
    //             {
    //                 progress = 1 - loopProgress;
    //             }
    //         }
    //         else
    //         {
    //             progress = loopProgress;
    //             if (loopType == LoopType.Incremental)
    //             {
    //                 offset = currentLoop * 1; //EaseUtility.Evaluate(1, ease);
    //             }
    //         }
    //     }
    //
    //     return offset + factor * EaseUtility.Evaluate(Math.Clamp(progress, 0, 1), ease);
    // }
}