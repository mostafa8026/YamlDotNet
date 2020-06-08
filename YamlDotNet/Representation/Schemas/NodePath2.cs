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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Helpers;

namespace YamlDotNet.Representation.Schemas
{
    public interface INodeMatcher
    {
        bool Matches(INodePathSegment node);
        IEnumerable<INodeMatcher> Children { get; }
        INodeMapper Mapper { get; }

        void AddStringRepresentation(StringBuilder output);
    }

    public abstract class NodeMatcher : INodeMatcher, IEnumerable
    {
        // Many matchers won't have children, hence the lazy initialization
        private List<INodeMatcher>? children;

        protected NodeMatcher(INodeMapper mapper)
        {
            Mapper = mapper;
        }

        public void Add(INodeMatcher child)
        {
            if (children == null)
            {
                children = new List<INodeMatcher>();
            }
            children.Add(child);
        }

        public IEnumerable<INodeMatcher> Children => children ?? Enumerable.Empty<INodeMatcher>();
        public INodeMapper Mapper { get; }

        public abstract bool Matches(INodePathSegment node);

        IEnumerator IEnumerable.GetEnumerator() => Children.GetEnumerator();

        public override string ToString()
        {
            static void ToString(INodeMatcher matcher, StringBuilder output, int indent, Dictionary<INodeMatcher, int> visitedNodeMatchers)
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
                    output.Append(matcher.Mapper);

                    foreach (var child in matcher.Children)
                    {
                        output.AppendLine();
                        ToString(child, output, indent + 1, visitedNodeMatchers);
                    }
                }
            }

            var text = new StringBuilder();
            ToString(this, text, 0, new Dictionary<INodeMatcher, int>(ReferenceEqualityComparer<INodeMatcher>.Default));
            return text.ToString();
        }

        public abstract void AddStringRepresentation(StringBuilder output);
    }

    public class NodeKindMatcher : NodeMatcher
    {
        public NodeKindMatcher(INodeMapper mapper) : base(mapper)
        {
        }

        public override bool Matches(INodePathSegment node) => node.Kind == Mapper.MappedNodeKind;

        public override void AddStringRepresentation(StringBuilder output)
        {
            output.Append(Mapper.MappedNodeKind);
        }
    }

    public sealed class TagMatcher : NodeKindMatcher
    {
        private readonly TagName expectedTag;

        public TagMatcher(TagName expectedTag, INodeMapper mapper) : base(mapper)
        {
            this.expectedTag = expectedTag;
        }

        public override bool Matches(INodePathSegment node)
        {
            return base.Matches(node)
                && node.Tag.Equals(expectedTag);
        }

        public override void AddStringRepresentation(StringBuilder output)
        {
            base.AddStringRepresentation(output);
            output
                .Append("[tag='")
                .Append(expectedTag)
                .Append("']");
        }
    }

    public sealed class NonSpecificTagMatcher : NodeKindMatcher
    {
        public NonSpecificTagMatcher(INodeMapper mapper) : base(mapper)
        {
        }

        public override bool Matches(INodePathSegment node)
        {
            return base.Matches(node)
                && node.Tag.IsNonSpecific;
        }

        public override void AddStringRepresentation(StringBuilder output)
        {
            base.AddStringRepresentation(output);
            output
                .Append("[tag=?|!]");
        }
    }

    public sealed class ScalarValueMatcher : NodeKindMatcher
    {
        private readonly string expectedScalarValue;

        public ScalarValueMatcher(string expectedScalarValue, INodeMapper mapper) : base(mapper)
        {
            if (mapper.MappedNodeKind != NodeKind.Scalar)
            {
                throw new ArgumentException("The specified mapper must be for scalars", nameof(mapper));
            }

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

    public sealed class RegexMatcher : NodeKindMatcher
    {
        private readonly Regex pattern;

        public RegexMatcher(string pattern, INodeMapper mapper)
            : this(new Regex(pattern, StandardRegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture), mapper)
        {
        }

        public RegexMatcher(Regex pattern, INodeMapper mapper) : base(mapper)
        {
            if (mapper.MappedNodeKind != NodeKind.Scalar)
            {
                throw new ArgumentException("The specified mapper must be for scalars", nameof(mapper));
            }

            this.pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        }

        public override bool Matches(INodePathSegment node)
        {
            return base.Matches(node)
                && node.Tag.IsEmpty
                && pattern.IsMatch(node.Value);
        }

        public override void AddStringRepresentation(StringBuilder output)
        {
            base.AddStringRepresentation(output);
            output
                .Append("[value~'")
                .Append(pattern.ToString())
                .Append("']");
        }
    }
}