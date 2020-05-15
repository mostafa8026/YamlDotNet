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

        /// <summary>
        /// A version of the <see cref="FailsafeSchema"/> that conforms strictly to the specification
        /// by not resolving any unrecognized scalars.
        /// </summary>
        public static readonly FailsafeSchema Strict = new FailsafeSchema(true);

        /// <summary>
        /// A version of the <see cref="FailsafeSchema"/> that treats unrecognized scalars as strings.
        /// </summary>
        public static readonly FailsafeSchema Lenient = new FailsafeSchema(false);

        public bool ResolveNonSpecificTag(Events.Scalar node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out IScalarMapper? resolvedTag)
        {
            if (node.Tag.IsEmpty && strict)
            {
                resolvedTag = null;
                return false;
            }

            resolvedTag = StringTag.Instance;
            return true;
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
            resolvedTag = StringTag.Instance;
            return tag.Equals(resolvedTag.Tag);
        }

        public bool IsTagImplicit(Scalar node, IEnumerable<CollectionEvent> path, out ScalarStyle style)
        {
            style = ScalarStyle.Any;
            return node.Tag.Equals(YamlTagRepository.String);
        }

        public bool IsTagImplicit(Mapping node, IEnumerable<CollectionEvent> path, out MappingStyle style)
        {
            style = MappingStyle.Any;
            return node.Tag.Equals(YamlTagRepository.Mapping);
        }

        public bool IsTagImplicit(Sequence node, IEnumerable<CollectionEvent> path, out SequenceStyle style)
        {
            style = SequenceStyle.Any;
            return node.Tag.Equals(YamlTagRepository.Sequence);
        }
    }
}
