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
        private readonly IScalarMapper? fallback;
        private readonly ScalarStyle nonPlainScalarStyle;
        private readonly SequenceStyle sequenceStyle;
        private readonly MappingStyle mappingStyle;

        protected RegexBasedSchema(
            RegexTagMappingTable tagMappingTable,
            IScalarMapper? fallback,
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

        public bool ResolveNonSpecificTag(Events.Scalar node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out IScalarMapper? resolvedTag)
        {
            if (!node.Tag.IsEmpty)
            {
                resolvedTag = StringTag.Instance;
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

        public bool ResolveNonSpecificTag(MappingStart node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out TagName resolvedTag)
        {
            resolvedTag = YamlTagRepository.Mapping;
            return true;
        }

        public bool ResolveNonSpecificTag(SequenceStart node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out TagName resolvedTag)
        {
            resolvedTag = YamlTagRepository.Sequence;
            return true;
        }

        public bool ResolveScalarMapper(TagName tag, [NotNullWhen(true)] out IScalarMapper? resolvedTag)
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

        public bool IsTagImplicit(Scalar node, IEnumerable<CollectionEvent> path, out ScalarStyle style)
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

        public bool IsTagImplicit(Mapping node, IEnumerable<CollectionEvent> path, out MappingStyle style)
        {
            style = mappingStyle;
            return node.Tag.Equals(YamlTagRepository.Mapping);
        }

        public bool IsTagImplicit(Sequence node, IEnumerable<CollectionEvent> path, out SequenceStyle style)
        {
            style = sequenceStyle;
            return node.Tag.Equals(YamlTagRepository.Sequence);
        }

        protected interface IRegexBasedScalarMapper : IScalarMapper
        {
            bool Matches(string value, [NotNullWhen(true)] out IScalarMapper? resultingTag);
        }

        private sealed class CompositeRegexBasedTag : IRegexBasedScalarMapper
        {
            private readonly IRegexBasedScalarMapper[] subTags;

            public TagName Tag { get; }

            public CompositeRegexBasedTag(TagName tag, IEnumerable<IRegexBasedScalarMapper> subTags)
            {
                Tag = tag;
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

                throw new SemanticErrorException($"The value '{value}' could not be parsed as '{Tag}'.");
            }

            public Scalar Represent(object? native)
            {
                // TODO: Review this
                return subTags.First().Represent(native);
            }

            public bool Matches(string value, [NotNullWhen(true)] out IScalarMapper? resultingTag)
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

        private sealed class RegexBasedScalarMapper : ScalarMapper, IRegexBasedScalarMapper
        {
            private readonly Regex pattern;

            public RegexBasedScalarMapper(TagName tag, Regex pattern, Func<Scalar, object?> constructor, Func<object?, string> representer)
                : base(tag, constructor, (t, v) => new Scalar(t, representer(v)))
            {
                if (representer is null)
                {
                    throw new ArgumentNullException(nameof(representer));
                }

                this.pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            }

            public bool Matches(string value, [NotNullWhen(true)] out IScalarMapper? resultingTag)
            {
                resultingTag = this;
                return pattern.IsMatch(value);
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