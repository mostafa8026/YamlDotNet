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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.Representation;
using YamlDotNet.Representation.Schemas;

namespace YamlDotNet.Serialization.Schemas
{
    public sealed class TypeSchema : ISchema
    {
        public TypeSchema(Type root, ISchema baseSchema, TypeMatcherTable typeMatchers)
        {
            var rootMatcher = typeMatchers.GetNodeMatcher(root);
            Root = new SchemaNode(new[] { rootMatcher });

            //ITagNameResolver resolver = TypeNameTagNameResolver.Instance;
            //if (!resolver.Resolve(root, out var rootTag))
            //{
            //    throw new Exception("TODO: Exception type and message");
            //}

            //Root = new SchemaNode(new NodeMatcher[]
            //{
            //    NodeMatcher
            //        .ForMappings(new ObjectMapper(root, rootTag))
            //        .Either(
            //            s => s.MatchTag(rootTag),
            //            s => s.MatchEmptyTags()
            //        )
            //        .Create(),
            //});
        }

        public ISchemaIterator Root { get; }

        private sealed class NodeMatcherIterator : ISchemaIterator
        {
            private readonly NodeMatcher matcher;

            public NodeMatcherIterator(NodeMatcher matcher)
            {
                this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            }

            public ISchemaIterator EnterMapping(IMapping mapping)
            {
                throw new NotImplementedException();
            }

            public ISchemaIterator EnterMappingValue()
            {
                // throw new NotImplementedException();
                return NullSchemaIterator.Instance; // TODO
            }

            public ISchemaIterator EnterScalar(IScalar scalar)
            {
                var childMatcher = matcher.Children.FirstOrDefault(c => c.Matches(scalar));
                return childMatcher != null
                    ? new NodeMatcherIterator(childMatcher)
                    : NullSchemaIterator.Instance;
            }

            public ISchemaIterator EnterSequence(ISequence sequence)
            {
                throw new NotImplementedException();
            }

            public bool IsTagImplicit(IScalar scalar, out ScalarStyle style)
            {
                throw new NotImplementedException();
            }

            public bool IsTagImplicit(ISequence sequence, out SequenceStyle style)
            {
                throw new NotImplementedException();
            }

            public bool IsTagImplicit(IMapping mapping, out MappingStyle style)
            {
                throw new NotImplementedException();
            }

            public bool TryResolveMapper(INode node, [NotNullWhen(true)] out INodeMapper? mapper)
            {
                mapper = matcher.Mapper;

                // No need to check whether the node matches,
                // because that test must necessarily have happened before in EnterMapping.
                return true;
            }
        }

        //private sealed 

        private sealed class SchemaNode : ISchemaIterator
        {
            private readonly IEnumerable<NodeMatcher> matchers;

            public SchemaNode(IEnumerable<NodeMatcher> matchers)
            {
                this.matchers = matchers;
            }

            public ISchemaIterator EnterScalar(IScalar scalar)
            {
                throw new NotImplementedException();
            }

            public ISchemaIterator EnterSequence(ISequence sequence)
            {
                throw new NotImplementedException();
            }

            public ISchemaIterator EnterMapping(IMapping mapping)
            {
                var matcher = matchers.First(m => m.Matches(mapping));
                return new NodeMatcherIterator(matcher);
            }

            public ISchemaIterator EnterMappingValue()
            {
                throw new NotImplementedException();
            }

            public bool IsTagImplicit(IScalar scalar, out ScalarStyle style)
            {
                throw new NotImplementedException();
            }

            public bool IsTagImplicit(ISequence sequence, out SequenceStyle style)
            {
                throw new NotImplementedException();
            }

            public bool IsTagImplicit(IMapping mapping, out MappingStyle style)
            {
                throw new NotImplementedException();
            }

            public bool TryResolveMapper(INode node, [NotNullWhen(true)] out INodeMapper? mapper)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class NullSchemaIterator : ISchemaIterator
        {
            private NullSchemaIterator() { }

            public static readonly ISchemaIterator Instance = new NullSchemaIterator();

            public ISchemaIterator EnterScalar(IScalar scalar) => this;
            public ISchemaIterator EnterSequence(ISequence sequence) => this;
            public ISchemaIterator EnterMapping(IMapping mapping) => this;
            public ISchemaIterator EnterMappingValue() => this;

            public bool TryResolveMapper(INode node, [NotNullWhen(true)] out INodeMapper? mapper)
            {
                mapper = default;
                return false;
            }

            public bool IsTagImplicit(IScalar scalar, out ScalarStyle style)
            {
                style = default;
                return false;
            }

            public bool IsTagImplicit(ISequence sequence, out SequenceStyle style)
            {
                style = default;
                return false;
            }

            public bool IsTagImplicit(IMapping mapping, out MappingStyle style)
            {
                style = default;
                return false;
            }
        }
    }

    //public sealed class TypeSchema : ISchema
    //{
    //    private readonly ISchema baseSchema;
    //    private readonly TypeMatcherTable typeMatchers;
    //    private readonly INodeMatcher rootMatcher;

    //    public TypeSchema(Type root, ISchema baseSchema, TypeMatcherTable typeMatchers)
    //    {
    //        this.baseSchema = baseSchema ?? throw new ArgumentNullException(nameof(baseSchema));
    //        this.typeMatchers = typeMatchers ?? throw new ArgumentNullException(nameof(typeMatchers));

    //        rootMatcher = typeMatchers.GetNodeMatcher(root);
    //    }

    //    public Document Represent(object? value)
    //    {
    //        var content = rootMatcher.Value.Represent(value, this, new NodePath());
    //        return new Document(content, this);
    //    }

    //    public override string ToString() => rootMatcher.ToString()!;

    //    public bool IsTagImplicit(Scalar node, IEnumerable<INodePathSegment> path, out ScalarStyle style)
    //    {
    //        if (rootMatcher.Query(path, out var mapper))
    //        {
    //            style = ScalarStyle.Plain;
    //            return node.Tag.Equals(mapper.Tag);
    //        }

    //        return this.baseSchema.IsTagImplicit(node, path, out style);
    //    }

    //    public bool IsTagImplicit(Mapping node, IEnumerable<INodePathSegment> path, out MappingStyle style)
    //    {
    //        if (rootMatcher.Query(path, out var mapper))
    //        {
    //            style = MappingStyle.Block;
    //            return node.Tag.Equals(mapper.Tag);
    //        }

    //        return this.baseSchema.IsTagImplicit(node, path, out style);
    //    }

    //    public bool IsTagImplicit(Sequence node, IEnumerable<INodePathSegment> path, out SequenceStyle style)
    //    {
    //        if (rootMatcher.Query(path, out var mapper))
    //        {
    //            style = SequenceStyle.Block;
    //            return node.Tag.Equals(mapper.Tag);
    //        }

    //        return this.baseSchema.IsTagImplicit(node, path, out style);
    //    }

    //    public bool ResolveMapper(TagName tag, [NotNullWhen(true)] out INodeMapper? mapper)
    //    {
    //        return typeMatchers.TryGetNodeMapper(tag, out mapper)
    //            || this.baseSchema.ResolveMapper(tag, out mapper);
    //    }

    //    public INodeMapper ResolveChildMapper(object? native, IEnumerable<INodePathSegment> path)
    //    {
    //        foreach (var mapper in rootMatcher.QueryChildren(path))
    //        {
    //            // TODO: Use the native value for something ?
    //            return mapper;
    //        }

    //        return this.baseSchema.ResolveChildMapper(native, path);
    //    }

    //    public bool ResolveNonSpecificTag(Core.Events.Scalar node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
    //    {
    //        if (rootMatcher.Query(path, out var mapper))
    //        {
    //            // TODO: Check the node itself ?
    //            resolvedTag = mapper;
    //            return true;
    //        }

    //        return this.baseSchema.ResolveNonSpecificTag(node, path, out resolvedTag);
    //    }

    //    public bool ResolveNonSpecificTag(MappingStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
    //    {
    //        if (rootMatcher.Query(path, out var mapper))
    //        {
    //            // TODO: Check the node itself ?
    //            resolvedTag = mapper;
    //            return true;
    //        }

    //        return this.baseSchema.ResolveNonSpecificTag(node, path, out resolvedTag);
    //    }

    //    public bool ResolveNonSpecificTag(SequenceStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
    //    {
    //        if (rootMatcher.Query(path, out var mapper))
    //        {
    //            // TODO: Check the node itself ?
    //            resolvedTag = mapper;
    //            return true;
    //        }

    //        return this.baseSchema.ResolveNonSpecificTag(node, path, out resolvedTag);
    //    }
    //}
}
