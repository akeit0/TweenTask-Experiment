// See https://aka.ms/new-console-template for more information

using TweenTasks;

using var provider = new TimerFrameDeltaTimeProvider(TimeSpan.FromSeconds(0.30));
TweenSystem.DefaultFrameDeltaTimeProvider = provider;
using var provider2 = new TimerFrameDeltaTimeProvider(TimeSpan.FromSeconds(0.10));
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
try
{
    var t1 = TweenTask.Create(0, 1, 3).Bind(static ( d) =>
            {
                // ReSharper disable once AccessToDisposedClosure
                Console.WriteLine($"progress 1:{d:f2}");
            }
        ).WithEase(Ease.OutCirc)
        .WithOnEvent(result => Console.WriteLine("Progress 1 Complete " + result)).Schedule();

    TweenTask.Create(0, 3, 1).Bind(
        static d => { Console.WriteLine($"progress 2:{d / 3:f2}"); },
        cts.Token)
        .WithOnEvent(result => Console.WriteLine("Progress 2 Complete " + result)).Schedule(provider2).Forget();

    TweenTask.Create(0, 1, 1).Bind(
            static d => { Console.WriteLine($"progress 3:{d:f2}"); },
            cts.Token)
        .WithOnEvent(result => Console.WriteLine("Progress 3 Complete " + result)).Schedule().Forget();

    var t4 = TweenTask.Create(0, 1, 2 - 0.01).Bind(provider,
        static (provider, d) => { Console.WriteLine($"progress 4:{d:f2}"); },
        cts.Token).Schedule();

    var t5 = TweenTask.Create(0, 1, 3 - 0.01).Bind(provider,
        static (provider, d) => { Console.WriteLine($"progress 5:{d:f2}"); },
        cts.Token).Schedule();;

    await t4;
    t1.TryCancel();
    await t5;
}
catch (Exception e)
{
    Console.WriteLine(e);
}


Console.WriteLine("End");