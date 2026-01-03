/*
MIT License
Copyright (c) Cysharp, Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace TweenTasks;

// mutable struct, don't mark readonly.
[StructLayout(LayoutKind.Auto)]
public struct TaskPool<T>
    where T : class, ITaskPoolNode<T>
{
    private int gate;
    private T? root;

    public int Size { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop([NotNullWhen(true)] out T? result)
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) == 0)
        {
            var v = root;
            if (!(v is null))
            {
                ref var nextNode = ref v.NextNode;
                root = nextNode;
                nextNode = null;
                Size--;
                result = v;
                Volatile.Write(ref gate, 0);
                return true;
            }

            Volatile.Write(ref gate, 0);
        }

        result = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPush(T item)
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) == 0)
        {
            if (Size < short.MaxValue)
            {
                item.NextNode = root;
                root = item;
                Size++;
                Volatile.Write(ref gate, 0);
                return true;
            }

            Volatile.Write(ref gate, 0);
        }

        return false;
    }
}