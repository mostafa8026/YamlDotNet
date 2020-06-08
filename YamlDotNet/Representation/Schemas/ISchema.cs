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

using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace YamlDotNet.Representation.Schemas
{
    public interface ISchemaIterator
    {
        ISchemaIterator EnterScalar(TagName tag, string value);
        ISchemaIterator EnterSequence(TagName tag);
        ISchemaIterator EnterMapping(TagName tag);
        ISchemaIterator EnterMappingValue();

        bool TryResolveMapper(NodeEvent node, [NotNullWhen(true)] out INodeMapper? mapper);

        bool IsTagImplicit(Scalar scalar, out ScalarStyle style);
        bool IsTagImplicit(Sequence sequence, out SequenceStyle style);
        bool IsTagImplicit(Mapping mapping, out MappingStyle style);
    }

    public static class SchemaIteratorExtensions
    {
        public static INodeMapper ResolveMapper(this ISchemaIterator iterator, NodeEvent node)
        {
            return iterator.TryResolveMapper(node, out var mapper)
                ? mapper
                : new UnresolvedTagMapper(node.Tag, node.Kind);
        }
    }

    public interface ISchema
    {
        ISchemaIterator Root { get; }

        ///// <summary>
        ///// Attempts to resolve the non-specific tag of the specified scalar.
        ///// </summary>
        ///// <param name="node">The scalar node for which the tag sould be resolved.</param>
        ///// <param name="path">An ordered sequence of the nodes that lead to this scalar (not including this one).</param>
        ///// <param name="resolvedTag">The resolved tag, if any.</param>
        ///// <returns>Returns true if the tag could be resolved; otherwise returns false.</returns>
        //bool ResolveNonSpecificTag(Events.Scalar node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag);

        ///// <summary>
        ///// Attempts to resolve the non-specific tag of the specified mapping.
        ///// </summary>
        ///// <param name="node">The mapping node for which the tag sould be resolved.</param>
        ///// <param name="path">An ordered sequence of the nodes that lead to this mapping (not including this one).</param>
        ///// <param name="resolvedTag">The resolved tag, if any.</param>
        ///// <returns>Returns true if the tag could be resolved; otherwise returns false.</returns>
        //bool ResolveNonSpecificTag(MappingStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag);

        ///// <summary>
        ///// Attempts to resolve the non-specific tag of the specified sequence.
        ///// </summary>
        ///// <param name="node">The sequence node for which the tag sould be resolved.</param>
        ///// <param name="path">An ordered sequence of the nodes that lead to this sequence (not including this one).</param>
        ///// <param name="resolvedTag">The resolved tag, if any.</param>
        ///// <returns>Returns true if the tag could be resolved; otherwise returns false.</returns>
        //bool ResolveNonSpecificTag(SequenceStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag);

        ///// <summary>
        ///// Determines whether the tag of the specified <paramref name="node"/> is implicit
        ///// according to this schema.
        ///// </summary>
        ///// <remarks>
        ///// Implicit tags are omitted from the presentation stream.
        ///// </remarks>
        ///// <param name="node">The scalar node for which the tag sould be resolved.</param>
        ///// <param name="path">An ordered sequence of the nodes that lead to this scalar (not including this one).</param>
        ///// <param name="style">The style that should be used if the tag is implicit.</param>
        ///// <returns>Returns true if the tag can be omitted; otherwise returns false.</returns>
        //bool IsTagImplicit(Scalar node, IEnumerable<INodePathSegment> path, out ScalarStyle style);

        ///// <summary>
        ///// Determines whether the tag of the specified <paramref name="node"/> is implicit
        ///// according to this schema.
        ///// </summary>
        ///// <remarks>
        ///// Implicit tags are omitted from the presentation stream.
        ///// </remarks>
        ///// <param name="node">The mapping node for which the tag sould be resolved.</param>
        ///// <param name="path">An ordered sequence of the nodes that lead to this scalar (not including this one).</param>
        ///// <param name="style">The style that should be used if the tag is implicit.</param>
        ///// <returns>Returns true if the tag can be omitted; otherwise returns false.</returns>
        //bool IsTagImplicit(Mapping node, IEnumerable<INodePathSegment> path, out MappingStyle style);

        ///// <summary>
        ///// Determines whether the tag of the specified <paramref name="node"/> is implicit
        ///// according to this schema.
        ///// </summary>
        ///// <remarks>
        ///// Implicit tags are omitted from the presentation stream.
        ///// </remarks>
        ///// <param name="node">The sequence node for which the tag sould be resolved.</param>
        ///// <param name="path">An ordered sequence of the nodes that lead to this scalar (not including this one).</param>
        ///// <param name="style">The style that should be used if the tag is implicit.</param>
        ///// <returns>Returns true if the tag can be omitted; otherwise returns false.</returns>
        //bool IsTagImplicit(Sequence node, IEnumerable<INodePathSegment> path, out SequenceStyle style);

        ///// <summary>
        ///// Attempts to resolve a specific <see cref="INodeMapper"/> for a given tag.
        ///// </summary>
        ///// <param name="tag">The tag as specified in the original YAMl document.</param>
        ///// <param name="mapper">The resolved <see cref="INodeMapper"/>, if any.</param>
        ///// <returns>Returns true if the mapper could be resolved; otherwise returns false.</returns>
        //bool ResolveMapper(TagName tag, [NotNullWhen(true)] out INodeMapper? mapper);

        //// TODO: Discontinue this API ?
        //INodeMapper ResolveChildMapper(object? native, IEnumerable<INodePathSegment> path);
    }
}