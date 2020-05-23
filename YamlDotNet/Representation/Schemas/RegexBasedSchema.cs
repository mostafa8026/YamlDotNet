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
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using Events = YamlDotNet.Core.Events;

namespace YamlDotNet.Representation.Schemas
{
    public abstract class RegexBasedSchema : ISchema
    {
        private readonly IDictionary<TagName, IRegexBasedScalarMapper> tags;
        private readonly INodeMapper? fallback;
        private readonly ScalarStyle nonPlainScalarStyle;
        private readonly SequenceStyle sequenceStyle;
        private readonly MappingStyle mappingStyle;

        protected RegexBasedSchema(
            RegexTagMappingTable tagMappingTable,
            INodeMapper? fallback,
            ScalarStyle nonPlainScalarStyle = ScalarStyle.SingleQuoted,
            SequenceStyle sequenceStyle = SequenceStyle.Any,
            MappingStyle mappingStyle = MappingStyle.Any
        )
        {
            this.tags = tagMappingTable
                .GroupBy(e => e.Tag)
                .Select(g => g.Count() switch
                {
                    1 => g.First(),
                    _ => new CompositeRegexBasedTag(g.Key, g)
                })
                .ToDictionary(e => e.Tag);

            this.fallback = fallback;

            if (nonPlainScalarStyle == ScalarStyle.Any || nonPlainScalarStyle == ScalarStyle.Plain)
            {
                throw new ArgumentException($"Invalid non-plain scalar style: '{nonPlainScalarStyle}'.", nameof(nonPlainScalarStyle));
            }

            this.nonPlainScalarStyle = nonPlainScalarStyle;
            this.sequenceStyle = sequenceStyle;
            this.mappingStyle = mappingStyle;
        }

        public bool ResolveNonSpecificTag(Events.Scalar node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
        {
            if (!node.Tag.IsEmpty)
            {
                resolvedTag = StringMapper.Default;
                return true;
            }

            var value = node.Value;
            foreach (var tag in tags.Values)
            {
                if (tag.Matches(value, out resolvedTag))
                {
                    return true;
                }
            }

            resolvedTag = fallback;
            return fallback != null;
        }

        public bool ResolveNonSpecificTag(MappingStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
        {
            resolvedTag = MappingMapper.Instance;
            return true;
        }

        public bool ResolveNonSpecificTag(SequenceStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
        {
            resolvedTag = SequenceMapper<object>.Default;
            return true;
        }

        public bool ResolveMapper(TagName tag, [NotNullWhen(true)] out INodeMapper? resolvedTag)
        {
            if (tags.TryGetValue(tag, out var result))
            {
                resolvedTag = result;
                return true;
            }
            else if (fallback != null && tag.Equals(fallback.Tag))
            {
                resolvedTag = fallback;
                return true;
            }

            resolvedTag = null;
            return false;
        }

        public bool IsTagImplicit(Scalar node, IEnumerable<INodePathSegment> path, out ScalarStyle style)
        {
            if (tags.TryGetValue(node.Tag, out var tag) && tag.Matches(node.Value, out _))
            {
                style = ScalarStyle.Plain;
                return true;
            }
            if (fallback != null && fallback.Tag.Equals(node.Tag))
            {
                style = node.Value.Length == 0 || tags.Any(t => t.Value.Matches(node.Value, out _))
                    ? nonPlainScalarStyle
                    : ScalarStyle.Any;

                return true;
            }
            style = ScalarStyle.Any;
            return false;
        }

        public bool IsTagImplicit(Mapping node, IEnumerable<INodePathSegment> path, out MappingStyle style)
        {
            style = mappingStyle;
            return node.Tag.Equals(YamlTagRepository.Mapping);
        }

        public bool IsTagImplicit(Sequence node, IEnumerable<INodePathSegment> path, out SequenceStyle style)
        {
            style = sequenceStyle;
            return node.Tag.Equals(YamlTagRepository.Sequence);
        }

        public INodeMapper ResolveMapper(object? native, IEnumerable<INodePathSegment> path)
        {
            throw new NotImplementedException();
        }

        protected interface IRegexBasedScalarMapper : INodeMapper
        {
            bool Matches(string value, [NotNullWhen(true)] out INodeMapper? resultingTag);
        }

        private sealed class CompositeRegexBasedTag : IRegexBasedScalarMapper
        {
            private readonly IRegexBasedScalarMapper[] subTags;

            public TagName Tag { get; }
            public NodeKind MappedNodeKind => NodeKind.Scalar;

            public CompositeRegexBasedTag(TagName tag, IEnumerable<IRegexBasedScalarMapper> subTags)
            {
                Tag = tag;
                this.subTags = subTags.ToArray();
            }

            public object? Construct(Node node)
            {
                var scalar = node.Expect<Scalar>();
                var value = scalar.Value;
                foreach (var subTag in subTags)
                {
                    if (subTag.Matches(value, out var resultingTag))
                    {
                        return resultingTag.Construct(node);
                    }
                }

                throw new SemanticErrorException($"The value '{value}' could not be parsed as '{Tag}'.");
            }

            public Node Represent(object? native, ISchema schema, NodePath currentPath)
            {
                // TODO: Review this
                return subTags.First().Represent(native, schema, currentPath);
            }

            public bool Matches(string value, [NotNullWhen(true)] out INodeMapper? resultingTag)
            {
                foreach (var subTag in subTags)
                {
                    if (subTag.Matches(value, out resultingTag))
                    {
                        return true;
                    }
                }

                resultingTag = null;
                return false;
            }
        }

        private sealed class RegexBasedScalarMapper : IRegexBasedScalarMapper
        {
            private readonly Regex pattern;
            private readonly Func<Scalar, object?> constructor;
            private readonly Func<object?, string> representer;

            public TagName Tag { get; }
            public NodeKind MappedNodeKind => NodeKind.Scalar;

            public RegexBasedScalarMapper(TagName tag, Regex pattern, Func<Scalar, object?> constructor, Func<object?, string> representer)
            {
                Tag = tag;
                this.pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
                this.constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
                this.representer = representer ?? throw new ArgumentNullException(nameof(representer));
            }

            public bool Matches(string value, [NotNullWhen(true)] out INodeMapper? resultingTag)
            {
                resultingTag = this;
                return pattern.IsMatch(value);
            }

            public object? Construct(Node node)
            {
                return this.constructor(node.Expect<Scalar>());
            }

            public Node Represent(object? native, ISchema schema, NodePath currentPath)
            {
                var value = representer(native);
                return new Scalar(this, value);
            }
        }

        protected sealed class RegexTagMappingTable : IEnumerable<IRegexBasedScalarMapper>
        {
            private readonly List<IRegexBasedScalarMapper> entries = new List<IRegexBasedScalarMapper>();

            public void Add(string pattern, TagName tag, Func<Scalar, object?> constructor, Func<object?, string> representer)
            {
                var regex = new Regex(pattern, StandardRegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);
                Add(regex, tag, constructor, representer);
            }

            public void Add(Regex pattern, TagName tag, Func<Scalar, object?> constructor, Func<object?, string> representer)
            {
                entries.Add(new RegexBasedScalarMapper(tag, pattern, constructor, representer));
            }

            public IEnumerator<IRegexBasedScalarMapper> GetEnumerator() => entries.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}