//  This file is part of YamlDotNet - A .NET library for YAML.
//  Copyright (c) Antoine Aubry and contributors

//  Permission is hereby granted, free of charge, to any person obtaining a copy of
//  this software and associated documentation files (the "Software"), to deal in
//  the Software without restriction, including without limitation the rights to
//  use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
//  of the Software, and to permit persons to whom the Software is furnished to do
//  so, subject to the following conditions:

//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.

//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.

using System;
using System.Collections.Generic;

namespace YamlDotNet.Core
{
    public sealed class NodePath
    {
        const int initialCapacity = 50;
        private INodePathSegment[] currentPath = new INodePathSegment[initialCapacity];
        private int count = 0;

        public IDisposable Push(INodePathSegment segment)
        {
            if (count == currentPath.Length)
            {
                Array.Resize(ref currentPath, currentPath.Length * 2);
            }

            currentPath[count] = segment;
            ++count;

            return new PopPath(this, count);
        }

        private sealed class PopPath : IDisposable
        {
            private readonly NodePath owner;
            private readonly int expectedCount;

            public PopPath(NodePath owner, int expectedCount)
            {
                this.owner = owner;
                this.expectedCount = expectedCount;
            }

            public void Dispose()
            {
                owner.Pop(expectedCount);
            }
        }

        private void Pop(int expectedCount)
        {
            if (count != expectedCount)
            {
                throw new InvalidOperationException($"Unbalanced call to Pop() was detected.");
            }

            --count;
        }

        public IEnumerable<INodePathSegment> GetCurrentPath()
        {
            // TODO: Evaluate whether this is the best approach
            var snapshot = new INodePathSegment[count];
            Array.Copy(currentPath, 0, snapshot, 0, count);
            return snapshot;
        }
    }
}