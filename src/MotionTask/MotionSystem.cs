using System;

namespace MotionTasks;

public static class  MotionSystem
{
    static Action<Exception> unhandledException = DefaultUnhandledExceptionHandler;
    public static FrameDeltaTimeProvider DefaultFrameDeltaTimeProvider { get; set; }

    // Prevent +=, use Set and Get method.
    public static void RegisterUnhandledExceptionHandler(Action<Exception> unhandledExceptionHandler)
    {
        unhandledException = unhandledExceptionHandler;
    }

    public static Action<Exception> GetUnhandledExceptionHandler()
    {
        return unhandledException;
    }

    static void DefaultUnhandledExceptionHandler(Exception exception)
    {
        Console.WriteLine("TweenTasks UnhandledException: " + exception.ToString());
    }
}