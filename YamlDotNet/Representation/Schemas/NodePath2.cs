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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Helpers;

namespace YamlDotNet.Representation.Schemas
{
    public interface INodeMatcher<T>
    {
        bool Matches(INodePathSegment node);
        IEnumerable<INodeMatcher<T>> Children { get; }
        T Value { get; }

        void AddStringRepresentation(StringBuilder output);
    }

    public abstract class NodeMatcher<T> : INodeMatcher<T>, IEnumerable
    {
        // Many matchers won't have children, hence the lazy initialization
        private List<INodeMatcher<T>>? children;

        protected NodeMatcher(T value)
        {
            Value = value;
        }

        public void Add(INodeMatcher<T> child)
        {
            if (children == null)
            {
                children = new List<INodeMatcher<T>>();
            }
            children.Add(child);
        }

        public IEnumerable<INodeMatcher<T>> Children => children ?? Enumerable.Empty<INodeMatcher<T>>();
        public T Value { get; }

        public abstract bool Matches(INodePathSegment node);

        IEnumerator IEnumerable.GetEnumerator() => Children.GetEnumerator();

        public override string ToString()
        {
            static void ToString(INodeMatcher<T> matcher, StringBuilder output, int indent, Dictionary<INodeMatcher<T>, int> visitedNodeMatchers)
            {
                for (int i = 0; i < indent; ++i)
                {
                    output.Append(' ');
                }
                if (visitedNodeMatchers.TryGetValue(matcher, out var id))
                {
                    output
                        .Append('*')
                        .Append(id);
                }
                else
                {
                    id = visitedNodeMatchers.Count + 1;
                    visitedNodeMatchers.Add(matcher, id);
                    output
                        .Append('#')
                        .Append(id)
                        .Append(' ');

                    matcher.AddStringRepresentation(output);
                    output.Append(" -> ");
                    output.Append(matcher.Value);

                    foreach (var child in matcher.Children)
                    {
                        output.AppendLine();
                        ToString(child, output, indent + 1, visitedNodeMatchers);
                    }
                }
            }

            var text = new StringBuilder();
            ToString(this, text, 0, new Dictionary<INodeMatcher<T>, int>(ReferenceEqualityComparer<INodeMatcher<T>>.Default));
            return text.ToString();
        }

        public abstract void AddStringRepresentation(StringBuilder output);
    }

    public class NodeKindMatcher<T> : NodeMatcher<T>
    {
        private readonly NodeKind expectedKind;

        public NodeKindMatcher(NodeKind expectedKind, T value) : base(value)
        {
            this.expectedKind = expectedKind;
        }

        public override bool Matches(INodePathSegment node) => node.Kind == expectedKind;

        public override void AddStringRepresentation(StringBuilder output)
        {
            output.Append(expectedKind);
        }
    }

    public sealed class ScalarValueMatcher<T> : NodeKindMatcher<T>
    {
        private readonly string expectedScalarValue;

        public ScalarValueMatcher(string expectedScalarValue, T value) : base(NodeKind.Scalar, value)
        {
            this.expectedScalarValue = expectedScalarValue ?? throw new ArgumentNullException(nameof(expectedScalarValue));
        }

        public override bool Matches(INodePathSegment node)
        {
            return base.Matches(node)
                && node.Value.Equals(expectedScalarValue);
        }

        public override void AddStringRepresentation(StringBuilder output)
        {
            base.AddStringRepresentation(output);
            output
                .Append("[value='")
                .Append(expectedScalarValue)
                .Append("']");
        }
    }

    //public sealed class SequenceMatcher<T>

    public static class NodePathMatcher
    {
        public static bool Query<T>(this INodeMatcher<T> matcher, IEnumerable<INodePathSegment> query, [MaybeNullWhen(false)] out T value)
        {
            if (Query(new[] { matcher }, query, out var result))
            {
                value = result.Value;
                return true;
            }

            value = default!;
            return false;
        }

        public static IEnumerable<T> QueryChildren<T>(this INodeMatcher<T> matcher, IEnumerable<INodePathSegment> query)
        {
            if (Query(new[] { matcher }, query, out var result))
            {
                foreach (var child in result.Children)
                {
                    yield return child.Value;
                }
            }
        }

        private static bool Query<T>(IEnumerable<INodeMatcher<T>> matchers, IEnumerable<INodePathSegment> query, [NotNullWhen(true)] out INodeMatcher<T>? result)
        {
            // I couldn't find a cleaner way to implement this algorithm without using gotos.

            using var iterator = query.GetEnumerator();

            if (iterator.MoveNext())
            {
                INodeMatcher<T> current;
                do
                {
                    var segment = iterator.Current;
                    foreach (var candidate in matchers)
                    {
                        if (candidate.Matches(segment))
                        {
                            current = candidate;
                            matchers = candidate.Children;
                            goto continue_traversal; // break from the inner loop and continue the outer loop
                        }
                    }
                    result = null;
                    return false;

                continue_traversal:;
                } while (iterator.MoveNext());

                result = current;
                return true;
            }

            result = null;
            return false;
        }
    }
}