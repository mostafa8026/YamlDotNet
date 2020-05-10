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
        private readonly bool strict;

        private FailsafeSchema(bool strict)
        {
            this.strict = strict;
        }

        public static readonly ITag<Scalar> String = new SimpleTag<Scalar>(YamlTagRepository.String, s => s.Value);
        public static readonly ITag<Mapping> Mapping = new SimpleTag<Mapping>(YamlTagRepository.Mapping, _ => throw new NotImplementedException());
        public static readonly ITag<Sequence> Sequence = new SimpleTag<Sequence>(YamlTagRepository.Sequence, _ => throw new NotImplementedException());

        /// <summary>
        /// A version of the <see cref="FailsafeSchema"/> that conforms strictly to the specification
        /// by not resolving any unrecognized scalars.
        /// </summary>
        public static readonly FailsafeSchema Strict = new FailsafeSchema(true);

        /// <summary>
        /// A version of the <see cref="FailsafeSchema"/> that treats unrecognized scalars as strings.
        /// </summary>
        public static readonly FailsafeSchema Lenient = new FailsafeSchema(false);

        public bool ResolveNonSpecificTag(Events.Scalar node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag<Scalar>? resolvedTag)
        {
            if (node.Tag.IsEmpty && strict)
            {
                resolvedTag = null;
                return false;
            }

            resolvedTag = String;
            return true;
        }

        public bool ResolveNonSpecificTag(MappingStart node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag<Mapping>? resolvedTag)
        {
            resolvedTag = Mapping;
            return true;
        }

        public bool ResolveNonSpecificTag(SequenceStart node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag<Sequence>? resolvedTag)
        {
            resolvedTag = Sequence;
            return true;
        }

        public bool ResolveSpecificTag(TagName tag, [NotNullWhen(true)] out ITag<Scalar>? resolvedTag)
        {
            resolvedTag = String;
            return tag.Equals(String.Name);
        }

        public bool ResolveSpecificTag(TagName tag, [NotNullWhen(true)] out ITag<Sequence>? resolvedTag)
        {
            resolvedTag = Sequence;
            return tag.Equals(Sequence.Name);
        }

        public bool ResolveSpecificTag(TagName tag, [NotNullWhen(true)] out ITag<Mapping>? resolvedTag)
        {
            resolvedTag = Mapping;
            return tag.Equals(Mapping.Name);
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
