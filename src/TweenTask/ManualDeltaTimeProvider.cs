using System;
using Cysharp.Threading.Tasks.Internal;

namespace TweenTasks;

public sealed class ManualFrameDeltaTimeProvider : FrameDeltaTimeProvider, IDisposable
{
    private bool disposed;
    const int InitialSize = 16;
    private long frameCount;
    private long lastFrameCount;
    readonly object runningAndQueueLock = new object();
    readonly object arrayLock = new object();

    int tail = 0;
    bool running = false;
    IFrameDeltaTimeProviderWorkItem?[] loopItems = new IFrameDeltaTimeProviderWorkItem[InitialSize];
    MinimumQueue<IFrameDeltaTimeProviderWorkItem> waitQueue = new MinimumQueue<IFrameDeltaTimeProviderWorkItem>(InitialSize);

    public ManualFrameDeltaTimeProvider(double currentTime)
    {
        lastFrameCount = -1;
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            lock (runningAndQueueLock)
            {
                loopItems.AsSpan().Slice(0, tail).Clear();
                waitQueue = null!;
            }
        }
    }

    public override long GetFrameCount()
    {
        return frameCount;
    }

    public long IncrementFrameCount()
    {
        lock (runningAndQueueLock)
        {
            return ++this.frameCount;
        }
    }

    public void UpdateFrame(long frameCount)
    {
        lock (runningAndQueueLock)
        {
            this.frameCount = frameCount;
        }
    }

    public override void Register(IFrameDeltaTimeProviderWorkItem item, bool forceNextFrame = true)
    {
        ThrowHelper.ThrowObjectDisposedIf(disposed, typeof(TimerFrameDeltaTimeProvider));
        lock (runningAndQueueLock)
        {
            if (running || (forceNextFrame && lastFrameCount != frameCount))
            {
                waitQueue.Enqueue(item);
                return;
            }
        }

        lock (arrayLock)
        {
            // Ensure Capacity
            if (loopItems.Length == tail)
            {
                Array.Resize(ref loopItems, checked(tail * 2));
            }

            loopItems[tail++] = item;
        }
    }

    public void Run(double deltaTime)
    {
        if (!(deltaTime >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        }

        if (disposed) return;

        long frameCount;
        lock (runningAndQueueLock)
        {
            running = true;
            frameCount = this.frameCount;
            if (lastFrameCount == frameCount)
            {
                throw new InvalidOperationException("UpdateFrame must be called before Run for each frame.");
            }

            this.lastFrameCount = frameCount;
           
        }

        lock (arrayLock)
        {
            var j = tail - 1;

            var info=new FrameInfo(frameCount, deltaTime);
            var loopItemsSpan = loopItems.AsSpan();
            for (var i = 0; i < loopItemsSpan.Length; i++)
            {
                ref var action = ref loopItemsSpan[i];
                if (action != null)
                {
                    try
                    {
                        if (!action.MoveNext(info))
                        {
                            action = null;
                        }
                        else
                        {
                            continue; // next i 
                        }
                    }
                    catch (Exception ex)
                    {
                        action = null;
                        try
                        {
                            TweenSystem.GetUnhandledExceptionHandler()(ex);
                        }
                        catch
                        {
                        }
                    }
                }

                // find null, loop from tail
                while (i < j)
                {
                    ref var fromTail = ref loopItemsSpan[j];
                    if (fromTail != null)
                    {
                        try
                        {
                            if (!fromTail.MoveNext(info))
                            {
                                fromTail = null;
                                j--;
                                continue; // next j
                            }
                            else
                            {
                                // swap
                                action = fromTail;
                                fromTail = null;
                                j--;
                                goto NEXT_LOOP; // next i
                            }
                        }
                        catch (Exception ex)
                        {
                            fromTail = null;
                            j--;
                            try
                            {
                                TweenSystem.GetUnhandledExceptionHandler()(ex);
                            }
                            catch
                            {
                            }

                            continue; // next j
                        }
                    }
                    else
                    {
                        j--;
                    }
                }

                tail = i; // loop end
                break; // LOOP END

                NEXT_LOOP:
                continue;
            }


            lock (runningAndQueueLock)
            {
                running = false;
                while (waitQueue.Count != 0)
                {
                    if (loopItems.Length == tail)
                    {
                        Array.Resize(ref loopItems, checked(tail * 2));
                    }

                    loopItems[tail++] = waitQueue.Dequeue();
                }
            }
        }
    }
}