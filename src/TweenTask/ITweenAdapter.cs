using System;

namespace TweenTasks;

public interface ITweenAdapter<in TOption,  T> : ITweenAdapter<T>
{
    void WithOption(TOption option);
}


public interface ITweenAdapter<T> 
{
    T Evaluate(double progress);
    T? From =>default(T);
    void ApplyFrom(T from,bool isRelative){}
}

public interface IRelativeAdapter<T>;
public interface ITweenFromAdapter<T>:ITweenAdapter<T> ;