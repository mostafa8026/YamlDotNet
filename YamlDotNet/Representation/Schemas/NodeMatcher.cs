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
    public abstract class NodeMatcher : IEnumerable
    {
        // Many matchers won't have children, hence the lazy initialization
        private List<NodeMatcher>? children;

        // Constructor is internal to prevent extensibility
        internal NodeMatcher(INodeMapper mapper)
        {
            Mapper = mapper;
        }

        public void Add(NodeMatcher child)
        {
            if (children == null)
            {
                children = new List<NodeMatcher>();
            }
            children.Add(child);
        }

        public IEnumerable<NodeMatcher> Children => children ?? Enumerable.Empty<NodeMatcher>();
        public INodeMapper Mapper { get; }

        IEnumerator IEnumerable.GetEnumerator() => Children.GetEnumerator();

        public abstract bool Matches(INode node);

        public override string ToString()
        {
            static void ToString(NodeMatcher matcher, StringBuilder output, int indent, Dictionary<NodeMatcher, int> visitedNodeMatchers)
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
            ToString(this, text, 0, new Dictionary<NodeMatcher, int>(ReferenceEqualityComparer<NodeMatcher>.Default));
            return text.ToString();
        }

        public abstract void AddStringRepresentation(StringBuilder output);

        public static IScalarNodeMatcherBuilderSyntax ForScalars(INodeMapper mapper, ScalarStyle suggestedStyle = ScalarStyle.Any)
        {
            return new ScalarMatcherBuilderSyntax(mapper, suggestedStyle);
        }

        public static INodeMatcherBuilderSyntax<ISequence, SequenceMatcher> ForSequences(INodeMapper mapper, SequenceStyle suggestedStyle = SequenceStyle.Any)
        {
            return new SequenceMatcherBuilderSyntax(mapper, suggestedStyle);
        }

        public static INodeMatcherBuilderSyntax<IMapping, MappingMatcher> ForMappings(INodeMapper mapper, MappingStyle suggestedStyle = MappingStyle.Any)
        {
            return new MappingMatcherBuilderSyntax(mapper, suggestedStyle);
        }

        private abstract class NodeMatcherBuilderSyntax<TNode, TMatcher, TStyle> : INodeMatcherBuilderSyntax<TNode, TMatcher>
            where TNode : class, INode
            where TStyle : Enum
        {
            private readonly List<INodePredicate<TNode>> predicates = new List<INodePredicate<TNode>>();
            private readonly INodeMapper mapper;
            private readonly TStyle suggestedStyle;

            public NodeMatcherBuilderSyntax(INodeMapper mapper, TStyle suggestedStyle)
            {
                this.mapper = mapper;
                this.suggestedStyle = suggestedStyle;
            }

            protected void AddPredicate(INodePredicate<TNode> predicate)
            {
                predicates.Add(predicate);
            }

            public INodeMatcherBuilderSyntax<TNode, TMatcher> MatchNonSpecificTags()
            {
                AddPredicate(NonSpecificTagMatcher.Instance);
                return this;
            }

            public INodeMatcherBuilderSyntax<TNode, TMatcher> MatchEmptyTags()
            {
                AddPredicate(EmptyTagMatcher.Instance);
                return this;
            }

            public INodeMatcherBuilderSyntax<TNode, TMatcher> MatchAnyNonSpecificTags()
            {
                AddPredicate(AnyNonSpecificTagMatcher.Instance);
                return this;
            }

            public INodeMatcherBuilderSyntax<TNode, TMatcher> MatchTag(TagName expectedTag)
            {
                AddPredicate(new TagMatcher(expectedTag));
                return this;
            }

            public TMatcher Create() => Create(mapper, suggestedStyle, predicates);

            protected abstract TMatcher Create(INodeMapper mapper, TStyle suggestedStyle, IEnumerable<INodePredicate<TNode>> predicates);
        }

        private sealed class ScalarMatcherBuilderSyntax : NodeMatcherBuilderSyntax<IScalar, ScalarMatcher, ScalarStyle>, IScalarNodeMatcherBuilderSyntax
        {
            public ScalarMatcherBuilderSyntax(INodeMapper mapper, ScalarStyle suggestedStyle)
                : base(mapper, suggestedStyle)
            {
            }

            public INodeMatcherBuilderSyntax<IScalar, ScalarMatcher> MatchPattern(string pattern)
            {
                return MatchPattern(new Regex(pattern, StandardRegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture));
            }

            public INodeMatcherBuilderSyntax<IScalar, ScalarMatcher> MatchPattern(Regex pattern)
            {
                AddPredicate(new RegexMatcher(pattern));
                return this;
            }

            public INodeMatcherBuilderSyntax<IScalar, ScalarMatcher> MatchValue(string expectedValue)
            {
                AddPredicate(new ScalarValueMatcher(expectedValue));
                return this;
            }

            protected override ScalarMatcher Create(INodeMapper mapper, ScalarStyle suggestedStyle, IEnumerable<INodePredicate<IScalar>> predicates)
            {
                return new ScalarMatcher(mapper, suggestedStyle, predicates);
            }
        }

        private sealed class SequenceMatcherBuilderSyntax : NodeMatcherBuilderSyntax<ISequence, SequenceMatcher, SequenceStyle>
        {
            public SequenceMatcherBuilderSyntax(INodeMapper mapper, SequenceStyle suggestedStyle)
                : base(mapper, suggestedStyle)
            {
            }

            protected override SequenceMatcher Create(INodeMapper mapper, SequenceStyle suggestedStyle, IEnumerable<INodePredicate<ISequence>> predicates)
            {
                return new SequenceMatcher(mapper, suggestedStyle, predicates);
            }
        }

        private sealed class MappingMatcherBuilderSyntax : NodeMatcherBuilderSyntax<IMapping, MappingMatcher, MappingStyle>
        {
            public MappingMatcherBuilderSyntax(INodeMapper mapper, MappingStyle suggestedStyle)
                : base(mapper, suggestedStyle)
            {
            }

            protected override MappingMatcher Create(INodeMapper mapper, MappingStyle suggestedStyle, IEnumerable<INodePredicate<IMapping>> predicates)
            {
                return new MappingMatcher(mapper, suggestedStyle, predicates);
            }
        }
    }

    public interface IScalarNodeMatcherBuilderSyntax : INodeMatcherBuilderSyntax<IScalar, ScalarMatcher>
    {
        INodeMatcherBuilderSyntax<IScalar, ScalarMatcher> MatchValue(string expectedValue);
        INodeMatcherBuilderSyntax<IScalar, ScalarMatcher> MatchPattern(string pattern);
        INodeMatcherBuilderSyntax<IScalar, ScalarMatcher> MatchPattern(Regex pattern);
    }

    public interface INodeMatcherBuilderSyntax<TNode, TMatcher>
    {
        INodeMatcherBuilderSyntax<TNode, TMatcher> MatchTag(TagName expectedTag);
        INodeMatcherBuilderSyntax<TNode, TMatcher> MatchNonSpecificTags();
        INodeMatcherBuilderSyntax<TNode, TMatcher> MatchEmptyTags();
        INodeMatcherBuilderSyntax<TNode, TMatcher> MatchAnyNonSpecificTags();

        TMatcher Create();
    }

    public abstract class NodeKindMatcher<TNode, TStyle> : NodeMatcher
        where TNode : INode
        where TStyle : Enum
    {
        protected readonly ICollection<INodePredicate<TNode>> predicates;

        // Constructor is internal to prevent extensibility
        internal NodeKindMatcher(INodeMapper mapper, TStyle suggestedStyle, IEnumerable<INodePredicate<TNode>> predicates) : base(mapper)
        {
            if (mapper.MappedNodeKind != MatchedNodeKind)
            {
                throw new ArgumentException($"The specified mapper should be for {MatchedNodeKind} but instead was for '{mapper.MappedNodeKind}'.", nameof(mapper));
            }

            if (predicates is null)
            {
                throw new ArgumentNullException(nameof(predicates));
            }

            SuggestedStyle = Invariants.ValidEnum(suggestedStyle, nameof(suggestedStyle));
            this.predicates = predicates.ToList();
        }

        protected abstract NodeKind MatchedNodeKind { get; }

        public TStyle SuggestedStyle { get; }

        public override bool Matches(INode node) => node is TNode specificNode && Matches(specificNode);
        public virtual bool Matches(TNode node) => predicates.All(p => p.Matches(node));

        public override void AddStringRepresentation(StringBuilder output)
        {
            output.Append(MatchedNodeKind);
            if (predicates.Count != 0)
            {
                output.Append('[');
                foreach (var predicate in predicates)
                {
                    output.Append(predicate.ToString());
                    output.Append(", ");
                }
                output.Length -= 2; // Remove the last ", "
                output.Append(']');
            }
        }
    }

    /// <summary>
    /// An <see cref="IScalarMatcher" /> that matches any scalar.
    /// </summary>
    public class ScalarMatcher : NodeKindMatcher<IScalar, ScalarStyle>
    {
        public ScalarMatcher(INodeMapper mapper, ScalarStyle suggestedStyle, IEnumerable<INodePredicate<IScalar>> predicates) : base(mapper, suggestedStyle, predicates)
        {
            if (mapper.MappedNodeKind != NodeKind.Scalar)
            {
                throw new ArgumentException("The specified mapper must be for scalars", nameof(mapper));
            }
        }

        public bool MatchesContent(IScalar node) => predicates.OfType<IScalarValuePredicate>().All(p => p.Matches(node));

        protected override NodeKind MatchedNodeKind => NodeKind.Scalar;
    }

    /// <summary>
    /// An <see cref="ISequenceMatcher" /> that matches any sequence.
    /// </summary>
    public class SequenceMatcher : NodeKindMatcher<ISequence, SequenceStyle>
    {
        public SequenceMatcher(INodeMapper mapper, SequenceStyle suggestedStyle, IEnumerable<INodePredicate<ISequence>> predicates) : base(mapper, suggestedStyle, predicates)
        {
            if (mapper.MappedNodeKind != NodeKind.Sequence)
            {
                throw new ArgumentException("The specified mapper must be for sequences", nameof(mapper));
            }
        }

        protected override NodeKind MatchedNodeKind => NodeKind.Sequence;
    }

    /// <summary>
    /// An <see cref="IMappingMatcher" /> that matches any mapping.
    /// </summary>
    public class MappingMatcher : NodeKindMatcher<IMapping, MappingStyle>
    {
        public MappingMatcher(INodeMapper mapper, MappingStyle suggestedStyle, IEnumerable<INodePredicate<IMapping>> predicates) : base(mapper, suggestedStyle, predicates)
        {
            if (mapper.MappedNodeKind != NodeKind.Mapping)
            {
                throw new ArgumentException("The specified mapper must be for mappings", nameof(mapper));
            }
        }

        protected override NodeKind MatchedNodeKind => NodeKind.Mapping;
    }

    public interface INodePredicate<in TNode> where TNode : INode
    {
        bool Matches(TNode node);
    }

    public interface IScalarValuePredicate : INodePredicate<IScalar> { }

    public sealed class TagMatcher : INodePredicate<INode>
    {
        private readonly TagName expectedTag;

        public TagMatcher(TagName expectedTag)
        {
            this.expectedTag = expectedTag;
        }

        public bool Matches(INode node) => node.Tag.Equals(expectedTag);
        public override string ToString() => $"tag='{expectedTag}'";
    }

    public sealed class NonSpecificTagMatcher : INodePredicate<INode>
    {
        private NonSpecificTagMatcher() { }

        public static readonly NonSpecificTagMatcher Instance = new NonSpecificTagMatcher();

        public bool Matches(INode node) => node.Tag.IsNonSpecific && !node.Tag.IsEmpty;
        public override string ToString() => "tag=!";
    }

    public sealed class EmptyTagMatcher : INodePredicate<INode>
    {
        private EmptyTagMatcher() { }

        public static readonly EmptyTagMatcher Instance = new EmptyTagMatcher();

        public bool Matches(INode node) => node.Tag.IsEmpty;
        public override string ToString() => "tag=?";
    }

    public sealed class AnyNonSpecificTagMatcher : INodePredicate<INode>
    {
        private AnyNonSpecificTagMatcher() { }

        public static readonly AnyNonSpecificTagMatcher Instance = new AnyNonSpecificTagMatcher();

        public bool Matches(INode node) => node.Tag.IsNonSpecific;
        public override string ToString() => "tag=!|?";
    }

    public sealed class ScalarValueMatcher : IScalarValuePredicate
    {
        private readonly string expectedValue;

        public ScalarValueMatcher(string expectedValue)
        {
            this.expectedValue = expectedValue ?? throw new ArgumentNullException(nameof(expectedValue));
        }

        public bool Matches(IScalar node) => node.Value.Equals(expectedValue);
        public override string ToString() => $"value='{expectedValue}'";
    }

    public sealed class RegexMatcher : IScalarValuePredicate
    {
        private readonly Regex pattern;

        public RegexMatcher(Regex pattern)
        {
            this.pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        }

        public bool Matches(IScalar node) => pattern.IsMatch(node.Value);
        public override string ToString() => $"value~'{pattern}'";
    }
}