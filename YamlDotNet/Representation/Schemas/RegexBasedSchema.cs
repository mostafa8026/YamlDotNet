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
        private readonly IDictionary<TagName, IRegexBasedTag> tags;
        private readonly ITag<Scalar>? fallbackTag;
        private readonly ScalarStyle nonPlainScalarStyle;
        private readonly SequenceStyle sequenceStyle;
        private readonly MappingStyle mappingStyle;

        protected RegexBasedSchema(
            RegexTagMappingTable tagMappingTable,
            ITag<Scalar>? fallbackTag,
            ScalarStyle nonPlainScalarStyle = ScalarStyle.SingleQuoted,
            SequenceStyle sequenceStyle = SequenceStyle.Any,
            MappingStyle mappingStyle = MappingStyle.Any
        )
        {
            this.tags = tagMappingTable
                .GroupBy(e => e.Name)
                .Select(g => g.Count() switch
                {
                    1 => g.First(),
                    _ => new CompositeRegexBasedTag(g.Key, g)
                })
                .ToDictionary(e => e.Name);

            this.fallbackTag = fallbackTag;

            if (nonPlainScalarStyle == ScalarStyle.Any || nonPlainScalarStyle == ScalarStyle.Plain)
            {
                throw new ArgumentException($"Invalid non-plain scalar style: '{nonPlainScalarStyle}'.", nameof(nonPlainScalarStyle));
            }

            this.nonPlainScalarStyle = nonPlainScalarStyle;
            this.sequenceStyle = sequenceStyle;
            this.mappingStyle = mappingStyle;
        }

        public bool ResolveNonSpecificTag(Events.Scalar node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag<Scalar>? resolvedTag)
        {
            if (!node.Tag.IsEmpty)
            {
                resolvedTag = FailsafeSchema.String;
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

            resolvedTag = fallbackTag;
            return fallbackTag != null;
        }

        public bool ResolveNonSpecificTag(MappingStart node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag<Mapping>? resolvedTag)
        {
            resolvedTag = FailsafeSchema.Mapping;
            return true;
        }

        public bool ResolveNonSpecificTag(SequenceStart node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag<Sequence>? resolvedTag)
        {
            resolvedTag = FailsafeSchema.Sequence;
            return true;
        }

        public bool ResolveSpecificTag(TagName tag, [NotNullWhen(true)] out ITag<Scalar>? resolvedTag)
        {
            if (tags.TryGetValue(tag, out var result))
            {
                resolvedTag = result;
                return true;
            }
            else if (fallbackTag != null && tag.Equals(fallbackTag.Name))
            {
                resolvedTag = fallbackTag;
                return true;
            }

            resolvedTag = null;
            return false;
        }

        public bool ResolveSpecificTag(TagName tag, [NotNullWhen(true)] out ITag<Sequence>? resolvedTag)
        {
            resolvedTag = FailsafeSchema.Sequence;
            return resolvedTag.Name.Equals(tag);
        }

        public bool ResolveSpecificTag(TagName tag, [NotNullWhen(true)] out ITag<Mapping>? resolvedTag)
        {
            resolvedTag = FailsafeSchema.Mapping;
            return resolvedTag.Name.Equals(tag);
        }

        public bool IsTagImplicit(Scalar node, IEnumerable<CollectionEvent> path, out ScalarStyle style)
        {
            if (tags.TryGetValue(node.Tag.Name, out var tag) && tag.Matches(node.Value, out _))
            {
                style = ScalarStyle.Plain;
                return true;
            }
            if (fallbackTag != null && fallbackTag.Name.Equals(node.Tag.Name))
            {
                style = node.Value.Length == 0 || tags.Any(t => t.Value.Matches(node.Value, out _))
                    ? nonPlainScalarStyle
                    : ScalarStyle.Any;

                return true;
            }
            style = ScalarStyle.Any;
            return false;
        }

        public bool IsTagImplicit(Mapping node, IEnumerable<CollectionEvent> path, out MappingStyle style)
        {
            style = mappingStyle;
            return node.Tag.Name.Equals(YamlTagRepository.Mapping);
        }

        public bool IsTagImplicit(Sequence node, IEnumerable<CollectionEvent> path, out SequenceStyle style)
        {
            style = sequenceStyle;
            return node.Tag.Name.Equals(YamlTagRepository.Sequence);
        }

        protected interface IRegexBasedTag : ITag<Scalar>
        {
            bool Matches(string value, [NotNullWhen(true)] out ITag<Scalar>? resultingTag);
        }

        private sealed class CompositeRegexBasedTag : IRegexBasedTag
        {
            private readonly IRegexBasedTag[] subTags;

            public TagName Name { get; }

            public CompositeRegexBasedTag(TagName name, IEnumerable<IRegexBasedTag> subTags)
            {
                Name = name;
                this.subTags = subTags.ToArray();
            }

            public object? Construct(Scalar node)
            {
                var value = node.Value;
                foreach (var subTag in subTags)
                {
                    if (subTag.Matches(value, out var resultingTag))
                    {
                        return resultingTag.Construct(node);
                    }
                }

                throw new SemanticErrorException($"The value '{value}' could not be parsed as '{Name}'.");
            }

            public Scalar Represent(object? native)
            {
                // TODO: Review this
                return subTags.First().Represent(native);
            }

            public bool Matches(string value, [NotNullWhen(true)] out ITag<Scalar>? resultingTag)
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

        private sealed class RegexBasedTag : SimpleTag<Scalar>, IRegexBasedTag
        {
            private readonly Regex pattern;

            public RegexBasedTag(TagName name, Regex pattern, Func<Scalar, object?> constructor, Func<object?, string> representer)
                : base(name, constructor, (t, v) => new Scalar(t, representer(v)))
            {
                if (representer is null)
                {
                    throw new ArgumentNullException(nameof(representer));
                }

                this.pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            }

            public bool Matches(string value, [NotNullWhen(true)] out ITag<Scalar>? resultingTag)
            {
                resultingTag = this;
                return pattern.IsMatch(value);
            }
        }

        protected sealed class RegexTagMappingTable : IEnumerable<IRegexBasedTag>
        {
            private readonly List<IRegexBasedTag> entries = new List<IRegexBasedTag>();

            public void Add(string pattern, TagName tag, Func<Scalar, object?> constructor, Func<object?, string> representer)
            {
                var regex = new Regex(pattern, StandardRegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);
                Add(regex, tag, constructor, representer);
            }

            public void Add(Regex pattern, TagName tag, Func<Scalar, object?> constructor, Func<object?, string> representer)
            {
                entries.Add(new RegexBasedTag(tag, pattern, constructor, representer));
            }

            public IEnumerator<IRegexBasedTag> GetEnumerator() => entries.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}