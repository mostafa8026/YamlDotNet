using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using Events = YamlDotNet.Core.Events;

namespace YamlDotNet.Representation.Schemas
{
    public interface ISchema
    {
        /// <summary>
        /// Attempts to resolve the non-specific tag of the specified scalar.
        /// </summary>
        /// <param name="node">The scalar node for which the tag sould be resolved.</param>
        /// <param name="path">An ordered sequence of the nodes that lead to this scalar (not including this one).</param>
        /// <param name="resolvedTag">The resolved tag, if any.</param>
        /// <returns>Returns true if the tag could be resolved; otherwise returns false.</returns>
        bool ResolveNonSpecificTag(Events.Scalar node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag<Scalar>? resolvedTag);

        /// <summary>
        /// Attempts to resolve the non-specific tag of the specified mapping.
        /// </summary>
        /// <param name="node">The mapping node for which the tag sould be resolved.</param>
        /// <param name="path">An ordered sequence of the nodes that lead to this mapping (not including this one).</param>
        /// <param name="resolvedTag">The resolved tag, if any.</param>
        /// <returns>Returns true if the tag could be resolved; otherwise returns false.</returns>
        bool ResolveNonSpecificTag(MappingStart node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag<Mapping>? resolvedTag);

        /// <summary>
        /// Attempts to resolve the non-specific tag of the specified sequence.
        /// </summary>
        /// <param name="node">The sequence node for which the tag sould be resolved.</param>
        /// <param name="path">An ordered sequence of the nodes that lead to this sequence (not including this one).</param>
        /// <param name="resolvedTag">The resolved tag, if any.</param>
        /// <returns>Returns true if the tag could be resolved; otherwise returns false.</returns>
        bool ResolveNonSpecificTag(SequenceStart node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag<Sequence>? resolvedTag);

        /// <summary>
        /// Determines whether the tag of the specified <paramref name="node"/> is implicit
        /// according to this schema.
        /// </summary>
        /// <remarks>
        /// Implicit tags are omitted from the presentation stream.
        /// </remarks>
        /// <param name="node">The scalar node for which the tag sould be resolved.</param>
        /// <param name="path">An ordered sequence of the nodes that lead to this scalar (not including this one).</param>
        /// <returns>Returns true if the tag can be omitted; otherwise returns false.</returns>
        bool IsTagImplicit(Events.Scalar node, IEnumerable<CollectionEvent> path);

        /// <summary>
        /// Determines whether the tag of the specified <paramref name="node"/> is implicit
        /// according to this schema.
        /// </summary>
        /// <remarks>
        /// Implicit tags are omitted from the presentation stream.
        /// </remarks>
        /// <param name="node">The mapping node for which the tag sould be resolved.</param>
        /// <param name="path">An ordered sequence of the nodes that lead to this scalar (not including this one).</param>
        /// <returns>Returns true if the tag can be omitted; otherwise returns false.</returns>
        bool IsTagImplicit(MappingStart node, IEnumerable<CollectionEvent> path);

        /// <summary>
        /// Determines whether the tag of the specified <paramref name="node"/> is implicit
        /// according to this schema.
        /// </summary>
        /// <remarks>
        /// Implicit tags are omitted from the presentation stream.
        /// </remarks>
        /// <param name="node">The sequence node for which the tag sould be resolved.</param>
        /// <param name="path">An ordered sequence of the nodes that lead to this scalar (not including this one).</param>
        /// <returns>Returns true if the tag can be omitted; otherwise returns false.</returns>
        bool IsTagImplicit(SequenceStart node, IEnumerable<CollectionEvent> path);

        /// <summary>
        /// Attempts to resolve a specific tag to enrich it with schema information.
        /// </summary>
        /// <param name="tag">The tag as specified in the original YAMl document.</param>
        /// <param name="resolvedTag">The resolved tag, if any.</param>
        /// <returns>Returns true if the tag could be resolved; otherwise returns false.</returns>
        bool ResolveSpecificTag(TagName tag, [NotNullWhen(true)] out ITag<Scalar>? resolvedTag);

        /// <summary>
        /// Attempts to resolve a specific tag to enrich it with schema information.
        /// </summary>
        /// <param name="tag">The tag as specified in the original YAMl document.</param>
        /// <param name="resolvedTag">The resolved tag, if any.</param>
        /// <returns>Returns true if the tag could be resolved; otherwise returns false.</returns>
        bool ResolveSpecificTag(TagName tag, [NotNullWhen(true)] out ITag<Sequence>? resolvedTag);

        /// <summary>
        /// Attempts to resolve a specific tag to enrich it with schema information.
        /// </summary>
        /// <param name="tag">The tag as specified in the original YAMl document.</param>
        /// <param name="resolvedTag">The resolved tag, if any.</param>
        /// <returns>Returns true if the tag could be resolved; otherwise returns false.</returns>
        bool ResolveSpecificTag(TagName tag, [NotNullWhen(true)] out ITag<Mapping>? resolvedTag);
    }
}