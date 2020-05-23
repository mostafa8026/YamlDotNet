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
using YamlDotNet.Core.Events;
using YamlDotNet.Helpers;
using YamlDotNet.Serialization.Utilities;

namespace YamlDotNet.Representation.Schemas
{
    public sealed class TypeSchema : ISchema
    {
        private readonly ISchema baseSchema;
        private readonly Dictionary<Type, Either<INodeMapper, Func<Type, INodeMapper>>> tagMappings;
        private readonly IDictionary<Type, NodeMatcher<X>> nodeMatchers;
        private readonly INodeMatcher<X> rootMatcher;

        class X
        {
            public INodeMapper Mapper { get; }

            public X(INodeMapper mapper)
            {
                Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            }

            public override string ToString() => Mapper.ToString()!;
        }

        public TypeSchema(Type root, ISchema baseSchema)
            : this(root, baseSchema, Enumerable.Empty<KeyValuePair<Type, Either<INodeMapper, Func<Type, INodeMapper>>>>())
        {
        }

        public TypeSchema(Type root, ISchema baseSchema, IEnumerable<KeyValuePair<Type, Either<INodeMapper, Func<Type, INodeMapper>>>> tagMappings)
        {
            this.baseSchema = baseSchema ?? throw new ArgumentNullException(nameof(baseSchema));

            this.tagMappings = new Dictionary<Type, Either<INodeMapper, Func<Type, INodeMapper>>>();
            foreach (var (type, mapper) in tagMappings)
            {
                if (mapper.IsEmpty)
                {
                    throw new ArgumentException("Tag mappings cannot contain empty values", nameof(tagMappings));
                }

                this.tagMappings.Add(type, mapper);
            }

            nodeMatchers = new Dictionary<Type, NodeMatcher<X>>();

            rootMatcher = BuildNodeMatcherTree(root, nodeMatchers);
        }

        private NodeMatcher<X> BuildNodeMatcherTree(Type type, IDictionary<Type, NodeMatcher<X>> nodeMatcherCache)
        {
            if (!nodeMatcherCache.TryGetValue(type, out var nodeMatcher))
            {
                foreach (var candidate in GetSuperTypes(type))
                {
                    var mapperFound = tagMappings.TryGetValue(candidate, out var mapperOrFactory);
                    if (!mapperFound && candidate.IsGenericType())
                    {
                        mapperFound = tagMappings.TryGetValue(candidate.GetGenericTypeDefinition(), out mapperOrFactory);
                    }

                    if (mapperFound)
                    {
                        if (!mapperOrFactory.GetValue(out var mapper, out var mapperFactory))
                        {
                            mapper = mapperFactory(candidate);
                        }

                        nodeMatcher = new NodeKindMatcher<X>(mapper.MappedNodeKind, new X(mapper));
                        nodeMatcherCache.Add(type, nodeMatcher);
                        return nodeMatcher;
                    }
                }

                // If we reach this point, then none of the configured tag mappings was able to handle this type.
                nodeMatcher = new NodeKindMatcher<X>(NodeKind.Mapping, new X(new ObjectMapper(type)));
                nodeMatcherCache.Add(type, nodeMatcher); // It is important to update the cache immediately to handle recursion

                // TODO: Type inspector
                foreach (var property in type.GetPublicProperties())
                {
                    // TODO: Naming convention
                    var keyName = property.Name;
                    nodeMatcher.Add(new ScalarValueMatcher<X>(keyName, new X(StringMapper.Default))
                    {
                        BuildNodeMatcherTree(property.PropertyType, nodeMatcherCache)
                    });
                }
            }

            return nodeMatcher;
        }

        private IEnumerable<Type> GetSuperTypes(Type type)
        {
            if (type.IsInterface())
            {
                yield return type;
            }
            else
            {
                Type? ancestor = type;
                while (ancestor != null)
                {
                    yield return ancestor;
                    ancestor = ancestor.BaseType();
                }
            }
            foreach (var itf in type.GetInterfaces())
            {
                yield return itf;
            }
        }

        public override string ToString() => rootMatcher.ToString()!;

        private class ObjectMapper : INodeMapper
        {
            private readonly Type type;

            public ObjectMapper(Type type) : this(type, YamlTagRepository.Mapping)
            {
            }

            public ObjectMapper(Type type, TagName tag)
            {
                this.type = type;
                Tag = tag;
            }

            public TagName Tag { get; }
            public NodeKind MappedNodeKind => NodeKind.Mapping;

            public object? Construct(Node node)
            {
                var mapping = node.Expect<Mapping>();

                // TODO: Pre-calculate the constructor(s) using expression trees
                var native = Activator.CreateInstance(type)!;
                foreach (var (keyNode, valueNode) in mapping)
                {
                    var key = keyNode.Mapper.Construct(keyNode);
                    var keyAsString = TypeConverter.ChangeType<string>(key);
                    // TODO: Naming convention
                    // TODO: Type inspector
                    var property = type.GetPublicProperty(keyAsString)
                        ?? throw new YamlException(keyNode.Start, keyNode.End, $"The property '{keyAsString}' was not found on type '{type.FullName}'."); // TODO: Exception type

                    var value = valueNode.Mapper.Construct(valueNode);
                    var convertedValue = TypeConverter.ChangeType(value, property.PropertyType);
                    property.SetValue(native, convertedValue, null);
                }
                return native;
            }

            public Node Represent(object? native, ISchema schema, NodePath currentPath)
            {
                if (native == null) // TODO: Do we need this ?
                {
                    return NullMapper.Default.NullScalar;
                }

                var children = new Dictionary<Node, Node>();
                var mapping = new Mapping(this, children.AsReadonlyDictionary());

                using (currentPath.Push(mapping))
                {
                    // TODO: Type inspector
                    var properties = native.GetType().GetPublicProperties();
                    foreach (var property in properties)
                    {
                        var key = property.Name; // TODO: Naming convention
                        var keyNode = new Scalar(StringMapper.Default, key);

                        using (currentPath.Push(keyNode))
                        {
                            var value = property.GetValue(native, null);
                            var valueMapper = schema.ResolveMapper(value, currentPath.GetCurrentPath());
                            var valueNode = valueMapper.Represent(value, schema, currentPath);

                            children.Add(keyNode, valueNode);
                        }
                    }
                }
                return mapping;
            }

            public override string ToString()
            {
                return $"[{Tag}] {type.FullName}";
            }
        }

        public bool IsTagImplicit(Scalar node, IEnumerable<INodePathSegment> path, out ScalarStyle style)
        {
            if (rootMatcher.Query(path, out var x))
            {
                style = ScalarStyle.Plain;
                return node.Tag.Equals(x.Mapper.Tag);
            }

            return this.baseSchema.IsTagImplicit(node, path, out style);
        }

        public bool IsTagImplicit(Mapping node, IEnumerable<INodePathSegment> path, out MappingStyle style)
        {
            if (rootMatcher.Query(path, out var x))
            {
                style = MappingStyle.Block;
                return node.Tag.Equals(x.Mapper.Tag);
            }

            return this.baseSchema.IsTagImplicit(node, path, out style);
        }

        public bool IsTagImplicit(Sequence node, IEnumerable<INodePathSegment> path, out SequenceStyle style)
        {
            if (rootMatcher.Query(path, out var x))
            {
                style = SequenceStyle.Block;
                return node.Tag.Equals(x.Mapper.Tag);
            }

            return this.baseSchema.IsTagImplicit(node, path, out style);
        }

        public bool ResolveMapper(TagName tag, [NotNullWhen(true)] out INodeMapper? mapper)
        {
            //if (tagMappings.TryGetKey(tag, out var type) && nodeMatchers.TryGetValue(type, out var matcher))
            //{
            //    mapper = matcher.Value.Mapper;
            //    return true;
            //}

            //return this.baseSchema.ResolveMapper(tag, out mapper);
            throw new NotImplementedException("TODO");
        }

        public INodeMapper ResolveMapper(object? native, IEnumerable<INodePathSegment> path)
        {
            if (rootMatcher.Query(path, out var x))
            {
                // TODO: Use the native value for something ?
                return x.Mapper;
            }

            return this.baseSchema.ResolveMapper(native, path);
        }

        public bool ResolveNonSpecificTag(Core.Events.Scalar node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
        {
            if (rootMatcher.Query(path, out var x))
            {
                // TODO: Check the node itself ?
                resolvedTag = x.Mapper;
                return true;
            }

            return this.baseSchema.ResolveNonSpecificTag(node, path, out resolvedTag);
        }

        public bool ResolveNonSpecificTag(MappingStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
        {
            if (rootMatcher.Query(path, out var x))
            {
                // TODO: Check the node itself ?
                resolvedTag = x.Mapper;
                return true;
            }

            return this.baseSchema.ResolveNonSpecificTag(node, path, out resolvedTag);
        }

        public bool ResolveNonSpecificTag(SequenceStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
        {
            if (rootMatcher.Query(path, out var x))
            {
                // TODO: Check the node itself ?
                resolvedTag = x.Mapper;
                return true;
            }

            return this.baseSchema.ResolveNonSpecificTag(node, path, out resolvedTag);
        }
    }
}
