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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Helpers;

namespace YamlDotNet.Representation.Schemas
{
    public abstract class NodeMatcher
    {
        // Constructor is internal to prevent extensibility
        internal NodeMatcher(INodeMapper mapper)
        {
            Mapper = mapper;
        }

        public INodeMapper Mapper { get; }

        public abstract bool Matches(INode node);
        public abstract bool Matches(Type type);

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

                    switch (matcher)
                    {
                        case SequenceMatcher sequenceMatcher:
                            foreach (var itemMatcher in sequenceMatcher.ItemMatchers)
                            {
                                output.AppendLine();
                                ToString(itemMatcher, output, indent + 1, visitedNodeMatchers);
                            }
                            break;

                        case MappingMatcher mappingMatcher:
                            foreach (var (keyMatcher, valueMatchers) in mappingMatcher.ItemMatchers)
                            {
                                output.AppendLine();
                                ToString(keyMatcher, output, indent + 1, visitedNodeMatchers);
                                foreach (var valueMatcher in valueMatchers)
                                {
                                    output.AppendLine();
                                    ToString(valueMatcher, output, indent + 2, visitedNodeMatchers);
                                }
                            }
                            break;
                    }
                }
            }

            var text = new StringBuilder();
            ToString(this, text, 0, new Dictionary<NodeMatcher, int>(ReferenceEqualityComparer<NodeMatcher>.Default));
            return text.ToString();
        }

        protected abstract void AddStringRepresentation(StringBuilder output);

        public static INodeMatcherBuilderSyntax<IScalar, ScalarMatcher> ForScalars(INodeMapper mapper, ScalarStyle suggestedStyle = ScalarStyle.Any)
        {
            if (mapper is null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            Invariants.ValidEnum(suggestedStyle, nameof(suggestedStyle));

            return new NodeMatcherBuilderSyntax<IScalar, ScalarMatcher>((nodePredicates, valuePredicates) => new ScalarMatcher(mapper, suggestedStyle, nodePredicates, valuePredicates));
        }

        public static INodeMatcherBuilderSyntax<ISequence, SequenceMatcher> ForSequences(INodeMapper mapper, SequenceStyle suggestedStyle = SequenceStyle.Any)
        {
            if (mapper is null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            Invariants.ValidEnum(suggestedStyle, nameof(suggestedStyle));

            return new NodeMatcherBuilderSyntax<ISequence, SequenceMatcher>((nodePredicates, valuePredicates) => new SequenceMatcher(mapper, suggestedStyle, nodePredicates, valuePredicates));
        }

        public static INodeMatcherBuilderSyntax<IMapping, MappingMatcher> ForMappings(INodeMapper mapper, MappingStyle suggestedStyle = MappingStyle.Any)
        {
            if (mapper is null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            Invariants.ValidEnum(suggestedStyle, nameof(suggestedStyle));

            return new NodeMatcherBuilderSyntax<IMapping, MappingMatcher>((nodePredicates, valuePredicates) => new MappingMatcher(mapper, suggestedStyle, nodePredicates, valuePredicates));
        }
    }

    internal class NodeMatcherBuilderSyntax<TNode> : INodeMatcherBuilderSyntax<TNode>, INodePredicate<TNode>
        where TNode : class, INode
    {
        protected readonly List<INodePredicate<TNode>> nodePredicates = new List<INodePredicate<TNode>>();
        protected readonly List<ITypePredicate> valuePredicates = new List<ITypePredicate>();

        public void AddPredicate(INodePredicate<TNode> predicate) => nodePredicates.Add(predicate);
        public void AddPredicate(INodePredicate<INode> predicate) => nodePredicates.Add(predicate);
        public void AddPredicate(ITypePredicate predicate) => valuePredicates.Add(predicate);

        public bool Matches(TNode node) => nodePredicates.All(p => p.Matches(node));

        public override string ToString()
        {
            var text = new StringBuilder();
            foreach (var predicate in nodePredicates)
            {
                text.Append(predicate);
                text.Append(", ");
            }
            if (text.Length > 2)
            {
                text.Length -= 2; // Remove the last ", "
            }
            return text.ToString();
        }
    }

    internal sealed class NodeMatcherBuilderSyntax<TNode, TMatcher> : NodeMatcherBuilderSyntax<TNode>, INodeMatcherBuilderSyntax<TNode, TMatcher>
        where TNode : class, INode
    {
        private readonly Func<IEnumerable<INodePredicate<TNode>>, IEnumerable<ITypePredicate>, TMatcher> factory;

        public NodeMatcherBuilderSyntax(Func<IEnumerable<INodePredicate<TNode>>, IEnumerable<ITypePredicate>, TMatcher> factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public TMatcher Create() => factory(nodePredicates, valuePredicates);
    }

    public interface INodeMatcherBuilderSyntax
    {
        void AddPredicate(INodePredicate<INode> predicate);
        void AddPredicate(ITypePredicate predicate);
    }

    public interface INodeMatcherBuilderSyntax<out TNode> : INodeMatcherBuilderSyntax
        where TNode : class, INode
    {
        void AddPredicate(INodePredicate<TNode> predicate);
    }

    public interface INodeMatcherBuilderSyntax<out TNode, TMatcher> : INodeMatcherBuilderSyntax<TNode>
        where TNode : class, INode
    {
        TMatcher Create();
    }

    public static class NodeMatcherBuilderSyntaxExtensions
    {
        public static TSyntax MatchTag<TSyntax>(this TSyntax syntax, TagName expectedTag)
            where TSyntax : INodeMatcherBuilderSyntax
        {
            syntax.AddPredicate(new TagMatcher(expectedTag));
            return syntax;
        }

        public static TSyntax MatchNonSpecificTags<TSyntax>(this TSyntax syntax)
            where TSyntax : INodeMatcherBuilderSyntax
        {
            syntax.AddPredicate(NonSpecificTagMatcher.Instance);
            return syntax;
        }

        public static TSyntax MatchEmptyTags<TSyntax>(this TSyntax syntax)
            where TSyntax : INodeMatcherBuilderSyntax
        {
            syntax.AddPredicate(EmptyTagMatcher.Instance);
            return syntax;
        }

        public static TSyntax MatchAnyNonSpecificTags<TSyntax>(this TSyntax syntax)
            where TSyntax : INodeMatcherBuilderSyntax
        {
            syntax.AddPredicate(AnyNonSpecificTagMatcher.Instance);
            return syntax;
        }

        public static TSyntax MatchTypes<TSyntax>(this TSyntax syntax, params Type[] types)
            where TSyntax : INodeMatcherBuilderSyntax
        {
            syntax.AddPredicate(new TypeMatcher(types));
            return syntax;
        }

        public static INodeMatcherBuilderSyntax Either(this INodeMatcherBuilderSyntax syntax, params Func<INodeMatcherBuilderSyntax, INodeMatcherBuilderSyntax>[] choices)
        {
            var predicates = choices
                .Select(c =>
                {
                    var childSyntax = new NodeMatcherBuilderSyntax<INode>();
                    c(childSyntax);
                    return (INodePredicate<INode>)childSyntax;
                });

            syntax.AddPredicate(new EitherMatcher<INode>(predicates));
            return syntax;
        }

        public static INodeMatcherBuilderSyntax<INode, ScalarMatcher> Either(this INodeMatcherBuilderSyntax<IScalar, ScalarMatcher> syntax, params Func<INodeMatcherBuilderSyntax<IScalar>, INodeMatcherBuilderSyntax>[] choices)
        {
            return EitherImpl(syntax, choices);
        }

        public static INodeMatcherBuilderSyntax<INode, SequenceMatcher> Either(this INodeMatcherBuilderSyntax<ISequence, SequenceMatcher> syntax, params Func<INodeMatcherBuilderSyntax<ISequence>, INodeMatcherBuilderSyntax>[] choices)
        {
            return EitherImpl(syntax, choices);
        }

        public static INodeMatcherBuilderSyntax<INode, MappingMatcher> Either(this INodeMatcherBuilderSyntax<IMapping, MappingMatcher> syntax, params Func<INodeMatcherBuilderSyntax<IMapping>, INodeMatcherBuilderSyntax>[] choices)
        {
            return EitherImpl(syntax, choices);
        }

        private static TSyntax EitherImpl<TNode, TSyntax>(TSyntax syntax, Func<INodeMatcherBuilderSyntax<TNode>, INodeMatcherBuilderSyntax>[] choices)
            where TNode : class, INode
            where TSyntax : INodeMatcherBuilderSyntax<TNode>
        {
            var predicates = choices
                .Select(c =>
                {
                    var childSyntax = new NodeMatcherBuilderSyntax<TNode>();
                    c(childSyntax);
                    return (INodePredicate<TNode>)childSyntax;
                });

            syntax.AddPredicate(new EitherMatcher<TNode>(predicates));
            return syntax;
        }

        public static TSyntax MatchValue<TSyntax>(this TSyntax syntax, string expectedValue)
            where TSyntax : INodeMatcherBuilderSyntax<IScalar>
        {
            syntax.AddPredicate(new ScalarValueMatcher(expectedValue));
            return syntax;
        }

        public static TSyntax MatchPattern<TSyntax>(this TSyntax syntax, string pattern)
            where TSyntax : class, INodeMatcherBuilderSyntax<IScalar>
        {
            return MatchPattern(syntax, new Regex(pattern, StandardRegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture));
        }

        public static TSyntax MatchPattern<TSyntax>(this TSyntax syntax, Regex pattern)
            where TSyntax : class, INodeMatcherBuilderSyntax<IScalar>
        {
            syntax.AddPredicate(new RegexMatcher(pattern));
            return syntax;
        }
    }

    public abstract class NodeKindMatcher<TNode, TStyle> : NodeMatcher
        where TNode : INode
        where TStyle : Enum
    {
        protected readonly ICollection<INodePredicate<TNode>> nodePredicates;
        protected readonly ICollection<ITypePredicate> typePredicates;

        // Constructor is internal to prevent extensibility
        internal NodeKindMatcher(INodeMapper mapper, TStyle suggestedStyle, IEnumerable<INodePredicate<TNode>> nodePredicates, IEnumerable<ITypePredicate> typePredicates) : base(mapper)
        {
            SuggestedStyle = Invariants.ValidEnum(suggestedStyle, nameof(suggestedStyle));

            if (nodePredicates is null)
            {
                throw new ArgumentNullException(nameof(nodePredicates));
            }

            this.nodePredicates = nodePredicates.ToList();

            if (typePredicates is null)
            {
                throw new ArgumentNullException(nameof(typePredicates));
            }

            this.typePredicates = typePredicates.ToList();
        }

        protected abstract NodeKind MatchedNodeKind { get; }

        public TStyle SuggestedStyle { get; }

        public override bool Matches(INode node) => node is TNode specificNode && Matches(specificNode);
        public virtual bool Matches(TNode node) => nodePredicates.All(p => p.Matches(node));
        public override bool Matches(Type type)
        {
            using var enumerator = typePredicates.GetEnumerator();
            if (enumerator.MoveNext())
            {
                do
                {
                    if (!enumerator.Current.Matches(type))
                    {
                        return false;
                    }
                } while (enumerator.MoveNext());
                return true;
            }

            // If there were no type predicates, do not match
            return false;
        }

        protected override void AddStringRepresentation(StringBuilder output)
        {
            output.Append(MatchedNodeKind);
            if (nodePredicates.Count != 0)
            {
                output.Append('[');
                foreach (var predicate in nodePredicates)
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
        public ScalarMatcher(INodeMapper mapper, ScalarStyle suggestedStyle, IEnumerable<INodePredicate<IScalar>> nodePredicates, IEnumerable<ITypePredicate> valuePredicates) : base(mapper, suggestedStyle, nodePredicates, valuePredicates)
        {
        }

        public bool MatchesContent(IScalar node) => nodePredicates.OfType<IScalarValuePredicate>().All(p => p.Matches(node));

        protected override NodeKind MatchedNodeKind => NodeKind.Scalar;
    }

    /// <summary>
    /// An <see cref="ISequenceMatcher" /> that matches any sequence.
    /// </summary>
    public class SequenceMatcher : NodeKindMatcher<ISequence, SequenceStyle>
    {
        private readonly List<NodeMatcher> itemMatchers = new List<NodeMatcher>();

        public SequenceMatcher(INodeMapper mapper, SequenceStyle suggestedStyle, IEnumerable<INodePredicate<ISequence>> nodePredicates, IEnumerable<ITypePredicate> valuePredicates) : base(mapper, suggestedStyle, nodePredicates, valuePredicates)
        {
        }

        protected override NodeKind MatchedNodeKind => NodeKind.Sequence;

        public IEnumerable<NodeMatcher> ItemMatchers => itemMatchers;

        public void AddItemMatcher(NodeMatcher itemMatcher)
        {
            if (itemMatcher is null)
            {
                throw new ArgumentNullException(nameof(itemMatcher));
            }

            itemMatchers.Add(itemMatcher);
        }
    }

    /// <summary>
    /// An <see cref="IMappingMatcher" /> that matches any mapping.
    /// </summary>
    public class MappingMatcher : NodeKindMatcher<IMapping, MappingStyle>
    {
        private readonly List<KeyValuePair<NodeMatcher, IEnumerable<NodeMatcher>>> itemMatchers = new List<KeyValuePair<NodeMatcher, IEnumerable<NodeMatcher>>>();

        public MappingMatcher(INodeMapper mapper, MappingStyle suggestedStyle, IEnumerable<INodePredicate<IMapping>> nodePredicates, IEnumerable<ITypePredicate> valuePredicates) : base(mapper, suggestedStyle, nodePredicates, valuePredicates)
        {
        }

        protected override NodeKind MatchedNodeKind => NodeKind.Mapping;

        public IEnumerable<KeyValuePair<NodeMatcher, IEnumerable<NodeMatcher>>> ItemMatchers => itemMatchers;

        public void AddItemMatcher(NodeMatcher keyMatcher, params NodeMatcher[] valueMatchers)
        {
            AddItemMatcher(keyMatcher, (IEnumerable<NodeMatcher>)valueMatchers);
        }

        public void AddItemMatcher(NodeMatcher keyMatcher, IEnumerable<NodeMatcher> valueMatchers)
        {
            if (keyMatcher is null)
            {
                throw new ArgumentNullException(nameof(keyMatcher));
            }

            if (valueMatchers is null)
            {
                throw new ArgumentNullException(nameof(valueMatchers));
            }

            itemMatchers.Add(new KeyValuePair<NodeMatcher, IEnumerable<NodeMatcher>>(keyMatcher, valueMatchers));
        }
    }

    public interface INodePredicate<in TNode> where TNode : INode
    {
        bool Matches(TNode node);
    }

    public interface ITypePredicate
    {
        bool Matches(Type type);
    }

    public interface IScalarValuePredicate : INodePredicate<IScalar> { }

    public sealed class EitherMatcher<TNode> : INodePredicate<TNode>
        where TNode : INode
    {
        private readonly INodePredicate<TNode>[] predicates;

        public EitherMatcher(IEnumerable<INodePredicate<TNode>> predicates)
        {
            if (predicates is null)
            {
                throw new ArgumentNullException(nameof(predicates));
            }

            this.predicates = predicates.ToArray();
        }

        public bool Matches(TNode node)
        {
            foreach (var predicate in predicates)
            {
                if (predicate.Matches(node))
                {
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append('(');
            foreach (var predicate in predicates)
            {
                text.Append(predicate);
                text.Append(" || ");
            }

            if (text.Length > 4)
            {
                text.Length -= 4; // Remove the last " || "
            }

            text.Append(')');

            return text.ToString();
        }
    }

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

    public sealed class TypeMatcher : ITypePredicate
    {
        private readonly Type[] types;

        public TypeMatcher(Type[] types)
        {
            this.types = types ?? throw new ArgumentNullException(nameof(types));
        }

        public bool Matches(Type type)
        {
            return types.Any(t => t.IsAssignableFrom(type));
        }
    }
}