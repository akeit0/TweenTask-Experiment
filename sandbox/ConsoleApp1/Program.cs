// See https://aka.ms/new-console-template for more information

using TweenTasks;

using var runner = new TimerTweenRunner(TimeSpan.FromSeconds(0.30));
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
try
{
    var t1 = TweenTask.Create(3, static (_, d) =>
        {
            // ReSharper disable once AccessToDisposedClosure
            Console.WriteLine($"progress 1:{d:f2}");
        }, null, runner,
        cts.Token).WithOnComplete((_, result) => Console.WriteLine("Progress 1 Complete " + result), null);

    TweenTask.Create(1, static (_, d) =>
        {
            Console.WriteLine($"progress 2:{d:f2}");
        }, null, runner,
        cts.Token).WithOnComplete((_, result) => Console.WriteLine("Progress 2 Complete " + result), cts).Forget();

    TweenTask.Create(5, static (_, d) =>
        {
            Console.WriteLine($"progress 3:{d:f2}");
        }, null, runner,
        cts.Token).WithOnComplete((_, result) => Console.WriteLine("Progress 3 Complete " + result), cts).Forget();
    
    var t4 = TweenTask.Create(2 - 0.01,
        static (runner, d) => { Console.WriteLine($"progress 4:{d:f2}, time:{runner.GetCurrentTime()}"); }, runner,
        runner, cts.Token);

    var t5 = TweenTask.Create(3 - 0.01,
        static (runner, d) => { Console.WriteLine($"progress 5:{d:f2}, time:{runner.GetCurrentTime()}"); }, runner,
        runner, cts.Token);

    await t4;
    t1.TryCancel();
    await t5;
}
catch (Exception e)
{
    Console.WriteLine(e);
}


Console.WriteLine("End");