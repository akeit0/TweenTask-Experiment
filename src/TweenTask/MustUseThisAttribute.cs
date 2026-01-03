using System;

namespace TweenTasks;

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
public class MustUseThisAttribute : Attribute { }