using System;

namespace MotionTasks;

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
public class MustUseThisAttribute(string arg) : Attribute { }