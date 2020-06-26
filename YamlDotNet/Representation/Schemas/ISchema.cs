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

using YamlDotNet.Core;

namespace YamlDotNet.Representation.Schemas
{
    public interface ISchemaIterator
    {
        ISchemaIterator EnterNode(INode node, out INodeMapper mapper);
        ISchemaIterator EnterValue(object? value, out INodeMapper mapper);
        ISchemaIterator EnterMappingValue();

        ///// <summary>
        ///// Attempts to resolve a mapper for the specified node.
        ///// </summary>
        ///// <remarks>
        ///// The <paramref name="node" /> that is passed will always be the same as
        ///// the one passed initially to access this instance, through the <see cref="EnterNode"/> method.
        ///// </remarks>
        //bool TryResolveMapper(INode node, [NotNullWhen(true)] out INodeMapper? mapper);

        bool IsTagImplicit(IScalar scalar, out ScalarStyle style);
        bool IsTagImplicit(ISequence sequence, out SequenceStyle style);
        bool IsTagImplicit(IMapping mapping, out MappingStyle style);
    }

    //public static class SchemaIteratorExtensions
    //{
    //    public static INodeMapper ResolveMapper(this ISchemaIterator iterator, INode node)
    //    {
    //        return iterator.TryResolveMapper(node, out var mapper)
    //            ? mapper
    //            : new UnresolvedTagMapper(node.Tag, node.Kind);
    //    }
    //}

    public interface ISchema
    {
        ISchemaIterator Root { get; }
    }
}