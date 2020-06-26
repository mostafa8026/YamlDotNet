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
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Helpers;
using YamlDotNet.Representation;
using YamlDotNet.Representation.Schemas;

namespace YamlDotNet.Serialization.Schemas
{
    public sealed class TypeSchema : ISchema
    {
        private readonly NodeMatcher rootMatcher;

        public TypeSchema(Type root, ISchema baseSchema, TypeMatcherTable typeMatchers)
        {
            rootMatcher = typeMatchers.GetNodeMatcher(root);
            Root = new MultipleNodeMatchersIterator(new[] { rootMatcher });
        }

        public override string ToString() => Root.ToString()!;

        public ISchemaIterator Root { get; }

        public Document Represent(object? value)
        {
            var iterator = Root.EnterValue(value, out var mapper);
            var content = mapper.Represent(value, iterator);

            return new Document(content, this);
        }

        private abstract class NodeMatcherIterator : ISchemaIterator
        {
            private readonly IEnumerable<NodeMatcher>? valueMatchers;

            public NodeMatcherIterator()
            {
            }

            public NodeMatcherIterator(IEnumerable<NodeMatcher> valueMatchers)
            {
                this.valueMatchers = valueMatchers ?? throw new ArgumentNullException(nameof(valueMatchers));
            }

            public abstract ISchemaIterator EnterNode(INode node, out INodeMapper mapper);
            public abstract ISchemaIterator EnterValue(object? value, out INodeMapper mapper);

            //public ISchemaIterator EnterNode(INode node, out INodeMapper mapper)
            //{
            //    var matcher = NodeMatchers.FirstOrDefault(m => m.Matches(node));
            //    if (matcher != null)
            //    {
            //        mapper = matcher.Mapper;
            //        return EnterMatcher(matcher);
            //    }
            //    else
            //    {
            //        mapper = new UnresolvedTagMapper(node.Tag, node.Kind);
            //        return NullSchemaIterator.Instance;
            //    }
            //}

            //public ISchemaIterator EnterValue(object? value, out INodeMapper mapper)
            //{
            //    var matcher = NodeMatchers.FirstOrDefault(m => m.Matches(value));
            //    if (matcher != null)
            //    {
            //        mapper = matcher.Mapper;
            //        return EnterMatcher(matcher);
            //    }
            //    else
            //    {
            //        mapper = new UnresolvedTagMapper(TagName.Empty, NodeKind.Mapping);
            //        return NullSchemaIterator.Instance;
            //    }
            //}

            //private ISchemaIterator EnterMatcher(NodeMatcher matcher)
            //{
            //    return matcher is MappingMatcher mappingMatcher
            //        ? new SingleNodeMatcherIterator(mappingMatcher, mappingMatcher.ItemMatchers)
            //        : new SingleNodeMatcherIterator(matcher);


            //    //switch (matcher)
            //    //{
            //    //    case SequenceMatcher sequenceMatcher:
            //    //        foreach (var itemMatcher in sequenceMatcher.ItemMatchers)
            //    //        {
            //    //            if (itemMatcher.Matches(node))
            //    //            {
            //    //                mapper = itemMatcher.Mapper;
            //    //                return new NodeMatcherIterator(itemMatcher);
            //    //            }
            //    //        }
            //    //        break;

            //    //    case MappingMatcher mappingMatcher:
            //    //        foreach (var (keyMatcher, valueMatchers) in mappingMatcher.ItemMatchers)
            //    //        {
            //    //            if (keyMatcher.Matches(node))
            //    //            {
            //    //                mapper = keyMatcher.Mapper;
            //    //                return new NodeMatcherIterator(keyMatcher, valueMatchers);
            //    //            }
            //    //        }
            //    //        break;

            //    //    case ScalarMatcher _:
            //    //        // Should not happen
            //    //        break;
            //    //}
            //}

            public ISchemaIterator EnterMappingValue()
            {
                return valueMatchers != null
                    ? new MultipleNodeMatchersIterator(valueMatchers)
                    : NullSchemaIterator.Instance;
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
        }

        private sealed class SingleNodeMatcherIterator : NodeMatcherIterator
        {
            private readonly NodeMatcher matcher;

            public SingleNodeMatcherIterator(NodeMatcher matcher)
            {
                this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            }

            public SingleNodeMatcherIterator(NodeMatcher matcher, IEnumerable<NodeMatcher> valueMatchers)
                : base(valueMatchers)
            {
                this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            }

            //public override IEnumerable<NodeMatcher> NodeMatchers => matcher switch
            //{
            //    SequenceMatcher sequenceMatcher => sequenceMatcher.ItemMatchers,
            //    MappingMatcher mappingMatcher => mappingMatcher.ItemMatchers.Select(m => m.Key),
            //    ScalarMatcher _ => Enumerable.Empty<NodeMatcher>(),
            //    _ => throw Invariants.InvalidCase(matcher),
            //};

            public override ISchemaIterator EnterNode(INode node, out INodeMapper mapper)
            {
                switch (matcher)
                {
                    case SequenceMatcher sequenceMatcher:
                        foreach (var itemMatcher in sequenceMatcher.ItemMatchers)
                        {
                            if (itemMatcher.Matches(node))
                            {
                                mapper = itemMatcher.Mapper;
                                return new SingleNodeMatcherIterator(itemMatcher);
                            }
                        }
                        break;

                    case MappingMatcher mappingMatcher:
                        foreach (var (keyMatcher, valueMatchers) in mappingMatcher.ItemMatchers)
                        {
                            if (keyMatcher.Matches(node))
                            {
                                mapper = keyMatcher.Mapper;
                                return new SingleNodeMatcherIterator(keyMatcher, valueMatchers);
                            }
                        }
                        break;

                    case ScalarMatcher _:
                        // Should not happen
                        break;
                }

                mapper = new UnresolvedTagMapper(node.Tag);
                return NullSchemaIterator.Instance;
            }

            public override ISchemaIterator EnterValue(object? value, out INodeMapper mapper)
            {
                switch (matcher)
                {
                    case SequenceMatcher sequenceMatcher:
                        foreach (var itemMatcher in sequenceMatcher.ItemMatchers)
                        {
                            if (itemMatcher.Matches(value))
                            {
                                mapper = itemMatcher.Mapper;
                                return new SingleNodeMatcherIterator(itemMatcher);
                            }
                        }
                        break;

                    case MappingMatcher mappingMatcher:
                        foreach (var (keyMatcher, valueMatchers) in mappingMatcher.ItemMatchers)
                        {
                            if (keyMatcher.Matches(value))
                            {
                                mapper = keyMatcher.Mapper;
                                return new SingleNodeMatcherIterator(keyMatcher, valueMatchers);
                            }
                        }
                        break;

                    case ScalarMatcher _:
                        // Should not happen
                        break;
                }

                mapper = new UnresolvedTagMapper(TagName.Empty);
                return NullSchemaIterator.Instance;
            }

            public override string ToString() => matcher.ToString();
        }

        private sealed class MultipleNodeMatchersIterator : NodeMatcherIterator
        {
            private IEnumerable<NodeMatcher> nodeMatchers;

            public MultipleNodeMatchersIterator(IEnumerable<NodeMatcher> nodeMatchers)
            {
                this.nodeMatchers = nodeMatchers ?? throw new ArgumentNullException(nameof(nodeMatchers));
            }

            public override ISchemaIterator EnterNode(INode node, out INodeMapper mapper)
            {
                var matcher = nodeMatchers.FirstOrDefault(m => m.Matches(node));
                if (matcher != null)
                {
                    mapper = matcher.Mapper;
                    return new SingleNodeMatcherIterator(matcher);
                }
                else
                {
                    mapper = new UnresolvedTagMapper(node.Tag);
                    return NullSchemaIterator.Instance;
                }
            }

            public override ISchemaIterator EnterValue(object? value, out INodeMapper mapper)
            {
                var matcher = nodeMatchers.FirstOrDefault(m => m.Matches(value));
                if (matcher != null)
                {
                    mapper = matcher.Mapper;
                    return new SingleNodeMatcherIterator(matcher);
                }
                else
                {
                    mapper = new UnresolvedTagMapper(TagName.Empty);
                    return NullSchemaIterator.Instance;
                }
            }

            public override string ToString() => string.Join(", ", nodeMatchers.Select(m => m.ToString()).ToArray());
        }

        private sealed class NullSchemaIterator : ISchemaIterator
        {
            private NullSchemaIterator() { }

            public static readonly ISchemaIterator Instance = new NullSchemaIterator();

            public IEnumerable<NodeMatcher> NodeMatchers => Enumerable.Empty<NodeMatcher>();

            public ISchemaIterator EnterNode(INode node, out INodeMapper mapper)
            {
                mapper = new UnresolvedTagMapper(node.Tag);
                return this;
            }

            public ISchemaIterator EnterValue(object? value, out INodeMapper mapper)
            {
                mapper = new UnresolvedTagMapper(TagName.Empty);
                return this;
            }

            //public ISchemaIterator EnterValue(object? value) => this;
            public ISchemaIterator EnterMappingValue() => this;

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
