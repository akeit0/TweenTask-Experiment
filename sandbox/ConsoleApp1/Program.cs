// See https://aka.ms/new-console-template for more information

using TweenTasks;

using var runner = new TimerTweenRunner(TimeSpan.FromSeconds(0.30));
ITweenRunner.Default = runner;
using var runner2 = new TimerTweenRunner(TimeSpan.FromSeconds(0.10));
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
try
{
    var t1 = TweenTask.Create(0, 1, 3).WithEase(Ease.OutCirc).Bind(static (_, d) =>
        {
            // ReSharper disable once AccessToDisposedClosure
            Console.WriteLine($"progress 1:{d:f2}");
        }, null
    ).WithOnComplete(result => Console.WriteLine("Progress 1 Complete " + result));

    TweenTask.Create(0, 3, 1).WithRunner(runner2).Bind(
        static d => { Console.WriteLine($"progress 2:{d / 3:f2}"); },
        cts.Token).WithOnComplete(result => Console.WriteLine("Progress 2 Complete " + result)).Forget();

    TweenTask.Create(0, 1, 1).Bind(static d => { Console.WriteLine($"progress 3:{d:f2}"); },
        cts.Token).WithOnComplete(result => Console.WriteLine("Progress 3 Complete " + result)).Forget();

    var t4 = TweenTask.Create(0, 1, 2 - 0.01).Bind(runner,
        static (runner, d) => { Console.WriteLine($"progress 4:{d:f2}, time:{runner.GetCurrentTime()}"); },
        cts.Token);

    var t5 = TweenTask.Create(0, 1, 3 - 0.01).Bind(runner,
        static (runner, d) => { Console.WriteLine($"progress 5:{d:f2}, time:{runner.GetCurrentTime()}"); },
        cts.Token);

    await t4;
    t1.TryCancel();
    await t5;
}
catch (Exception e)
{
    Console.WriteLine(e);
}


Console.WriteLine("End");