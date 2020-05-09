using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using Events = YamlDotNet.Core.Events;

namespace YamlDotNet.Representation.Schemas
{
    /// <summary>
    /// Implements the Failsafe schema: <see href="https://yaml.org/spec/1.2/spec.html#id2802346"/>.
    /// </summary>
    public sealed class FailsafeSchema : ISchema
    {
        private readonly ITag fallbackTag;

        private FailsafeSchema(ITag fallbackTag)
        {
            this.fallbackTag = fallbackTag ?? throw new ArgumentNullException(nameof(fallbackTag));
        }

        public static readonly ITag String = new SimpleTag(YamlTagRepository.String, s => s.Value);
        public static readonly ITag Mapping = new SimpleTag(YamlTagRepository.Mapping);
        public static readonly ITag Sequence = new SimpleTag(YamlTagRepository.Sequence);

        /// <summary>
        /// A version of the <see cref="FailsafeSchema"/> that conforms strictly to the specification
        /// by not resolving any unrecognized scalars.
        /// </summary>
        public static readonly FailsafeSchema Strict = new FailsafeSchema(new SimpleTag(TagName.Empty));

        /// <summary>
        /// A version of the <see cref="FailsafeSchema"/> that treats unrecognized scalars as strings.
        /// </summary>
        public static readonly FailsafeSchema Lenient = new FailsafeSchema(String);

        public bool ResolveNonSpecificTag(Events.Scalar node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag? resolvedTag)
        {
            if (node.Tag.IsEmpty)
            {
                resolvedTag = fallbackTag;
                return !fallbackTag.Name.IsEmpty;
            }

            resolvedTag = String;
            return true;
        }

        public bool ResolveNonSpecificTag(MappingStart node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag? resolvedTag)
        {
            if (node.Tag.IsEmpty)
            {
                resolvedTag = fallbackTag;
                return !fallbackTag.Name.IsEmpty;
            }

            resolvedTag = Mapping;
            return true;
        }

        public bool ResolveNonSpecificTag(SequenceStart node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag? resolvedTag)
        {
            if (node.Tag.IsEmpty)
            {
                resolvedTag = fallbackTag;
                return !fallbackTag.Name.IsEmpty;
            }

            resolvedTag = Sequence;
            return true;
        }

        public bool ResolveSpecificTag(TagName tag, [NotNullWhen(true)] out ITag? resolvedTag)
        {
            if (tag.Equals(String.Name))
            {
                resolvedTag = String;
                return true;
            }
            else if (tag.Equals(Sequence.Name))
            {
                resolvedTag = Sequence;
                return true;
            }
            else if (tag.Equals(Mapping.Name))
            {
                resolvedTag = Mapping;
                return true;
            }
            else
            {
                resolvedTag = null;
                return false;
            }
        }

        public bool IsTagImplicit(Events.Scalar node, IEnumerable<CollectionEvent> path)
        {
            return node.Tag.Equals(YamlTagRepository.String);
        }

        public bool IsTagImplicit(MappingStart node, IEnumerable<CollectionEvent> path)
        {
            return node.Tag.Equals(YamlTagRepository.Mapping);
        }

        public bool IsTagImplicit(SequenceStart node, IEnumerable<CollectionEvent> path)
        {
            return node.Tag.Equals(YamlTagRepository.Sequence);
        }
    }
}
