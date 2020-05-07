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
using YamlDotNet.Core.Events;

namespace YamlDotNet.Core
{
    /// <summary>
    /// An <see cref="IParsingEventVisitor" /> that tracks the path of collection
    /// nodes that lead to the current <see cref="NodeEvent"/>.
    /// </summary>
    public sealed class ParsingEventPathBuilder : IParsingEventVisitor<IEnumerable<CollectionEvent>>
    {
        private sealed class NodePath
        {
            const int initialCapacity = 50;
            private CollectionEvent[] currentPath = new CollectionEvent[initialCapacity];
            private int count = 0;
            private int version = 0;

            public void Push(CollectionEvent @event)
            {
                if (count == currentPath.Length)
                {
                    Array.Resize(ref currentPath, currentPath.Length * 2);
                }

                currentPath[count] = @event;
            }

            public void Pop()
            {
                if (count == 0)
                {
                    throw new InvalidOperationException($"Cannot pop elements from an empty NodePath");
                }

                --count;
                ++version; // Pop() invalidates previously returned enumerators
            }

            public IEnumerable<CollectionEvent> GetCurrentPath()
            {
                return Snapshot(count, version);
            }

            private IEnumerable<CollectionEvent> Snapshot(int length, int expectedVersion)
            {
                for (var i = 0; i < length; ++i)
                {
                    if (version != expectedVersion)
                    {
                        throw new InvalidOperationException("The NodePath from which this enumerator was build was modified.");
                    }

                    yield return currentPath[i];
                }
            }
        }

        private readonly NodePath currentPath = new NodePath();

        IEnumerable<CollectionEvent> IParsingEventVisitor<IEnumerable<CollectionEvent>>.Visit(AnchorAlias anchorAlias) => null!;
        IEnumerable<CollectionEvent> IParsingEventVisitor<IEnumerable<CollectionEvent>>.Visit(StreamStart streamStart) => null!;
        IEnumerable<CollectionEvent> IParsingEventVisitor<IEnumerable<CollectionEvent>>.Visit(StreamEnd streamEnd) => null!;
        IEnumerable<CollectionEvent> IParsingEventVisitor<IEnumerable<CollectionEvent>>.Visit(DocumentStart documentStart) => null!;
        IEnumerable<CollectionEvent> IParsingEventVisitor<IEnumerable<CollectionEvent>>.Visit(DocumentEnd documentEnd) => null!;
        IEnumerable<CollectionEvent> IParsingEventVisitor<IEnumerable<CollectionEvent>>.Visit(Comment comment) => null!;
        IEnumerable<CollectionEvent> IParsingEventVisitor<IEnumerable<CollectionEvent>>.Visit(Scalar scalar) => currentPath.GetCurrentPath();

        public IEnumerable<CollectionEvent> Visit(SequenceStart sequenceStart)
        {
            var path = currentPath.GetCurrentPath();
            currentPath.Push(sequenceStart);
            return path;
        }

        public IEnumerable<CollectionEvent> Visit(SequenceEnd sequenceEnd)
        {
            currentPath.Pop();
            return null!;
        }

        public IEnumerable<CollectionEvent> Visit(MappingStart mappingStart)
        {
            var path = currentPath.GetCurrentPath();
            currentPath.Push(mappingStart);
            return path;
        }

        public IEnumerable<CollectionEvent> Visit(MappingEnd mappingEnd)
        {
            currentPath.Pop();
            return null!;
        }
    }
}