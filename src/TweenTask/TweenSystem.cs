using System;

namespace TweenTasks
{
    public static class TweenSystem
    {
        static Action<Exception> unhandledException = DefaultUnhandledExceptionHandler;


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
}