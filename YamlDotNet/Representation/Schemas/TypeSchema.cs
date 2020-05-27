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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Helpers;
using YamlDotNet.Serialization.Utilities;
using NodeMatcherInstanceOrFactory = YamlDotNet.Helpers.Either<
    YamlDotNet.Representation.Schemas.INodeMatcher<YamlDotNet.Representation.INodeMapper>,
    YamlDotNet.Representation.Schemas.NodeMatcherFactory>;

namespace YamlDotNet.Representation.Schemas
{
    public delegate INodeMatcher<INodeMapper> NodeMatcherFactory(Type sourceType, Type matchedType, Func<Type, INodeMatcher<INodeMapper>> nodeMapperLookup);

    public sealed class TypeMatcherTable : IEnumerable<KeyValuePair<Type, NodeMatcherInstanceOrFactory>>
    {
        private readonly IDictionary<Type, INodeMatcher<INodeMapper>> nodeMatchers = new Dictionary<Type, INodeMatcher<INodeMapper>>();
        private readonly IDictionary<Type, NodeMatcherFactory> nodeMatcherFactories = new Dictionary<Type, NodeMatcherFactory>();

        public void Add(Type type, INodeMapper nodeMapper)
        {
            Add(type, new NodeKindMatcher<INodeMapper>(nodeMapper.MappedNodeKind, nodeMapper));
        }

        public void Add(Type type, INodeMatcher<INodeMapper> nodeMatcher)
        {
            nodeMatchers.Add(type, nodeMatcher);
        }

        public void Add(Type type, NodeMatcherFactory nodeMatcherFactory)
        {
            nodeMatcherFactories.Add(type, nodeMatcherFactory);
        }

        public IEnumerator<KeyValuePair<Type, NodeMatcherInstanceOrFactory>> GetEnumerator()
        {
            foreach (var (type, nodeMatcher) in nodeMatchers)
            {
                yield return new KeyValuePair<Type, NodeMatcherInstanceOrFactory>(type, nodeMatcher.AsEither());
            }
            foreach (var (type, nodeMatcherFactory) in nodeMatcherFactories)
            {
                yield return new KeyValuePair<Type, NodeMatcherInstanceOrFactory>(type, nodeMatcherFactory);
            }
        }

        public IDictionary<Type, INodeMatcher<INodeMapper>> GetNodeMatchers() => new Dictionary<Type, INodeMatcher<INodeMapper>>(nodeMatchers);
        public IDictionary<Type, NodeMatcherFactory> GetNodeMatcherFactories() => new Dictionary<Type, NodeMatcherFactory>(nodeMatcherFactories);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class TypeSchema : ISchema
    {
        private readonly ISchema baseSchema;
        private readonly IDictionary<Type, INodeMatcher<INodeMapper>> nodeMatchers;
        private readonly IDictionary<Type, NodeMatcherFactory> nodeMatcherFactories;
        private readonly INodeMatcher<INodeMapper> rootMatcher;

        private static readonly IDictionary<Type, INodeMatcher<INodeMapper>> EmptyNodeMatchers = new Dictionary<Type, INodeMatcher<INodeMapper>>();
        private static readonly IDictionary<Type, NodeMatcherFactory> EmptyNodeMatcherFactories = new Dictionary<Type, NodeMatcherFactory>();

        public TypeSchema(Type root, ISchema baseSchema)
            : this(root, baseSchema, EmptyNodeMatchers, EmptyNodeMatcherFactories)
        {
        }

        public TypeSchema(Type root, ISchema baseSchema, TypeMatcherTable typeMatchers)
            : this(root, baseSchema, typeMatchers.GetNodeMatchers(), typeMatchers.GetNodeMatcherFactories())
        {
        }

        private TypeSchema(Type root, ISchema baseSchema, IDictionary<Type, INodeMatcher<INodeMapper>> nodeMatchers, IDictionary<Type, NodeMatcherFactory> nodeMatcherFactories)
        {
            this.baseSchema = baseSchema ?? throw new ArgumentNullException(nameof(baseSchema));
            this.nodeMatchers = nodeMatchers;
            this.nodeMatcherFactories = nodeMatcherFactories;

            rootMatcher = BuildNodeMatcherTree(root);
        }

        public Document Represent(object? value)
        {
            var content = rootMatcher.Value.Represent(value, this, new NodePath());
            return new Document(content, this);
        }

        private INodeMatcher<INodeMapper> BuildNodeMatcherTree(Type type)
        {
            if (!nodeMatchers.TryGetValue(type, out var nodeMatcher))
            {
                if (TryLookupNodeMatcher(type, new Stack<Type>(2), out nodeMatcher))
                {
                    nodeMatchers.Add(type, nodeMatcher);
                    return nodeMatcher;
                }

                // If we reach this point, then none of the configured tag mappings was able to handle this type.
                var objectMatcher = new NodeKindMatcher<INodeMapper>(NodeKind.Mapping, new ObjectMapper(type));
                nodeMatchers.Add(type, objectMatcher); // It is important to update the cache immediately to handle recursion
                nodeMatcher = objectMatcher;

                // TODO: Type inspector
                foreach (var property in type.GetPublicProperties())
                {
                    // TODO: Naming convention
                    var keyName = property.Name;
                    objectMatcher.Add(new ScalarValueMatcher<INodeMapper>(keyName, StringMapper.Default)
                    {
                        BuildNodeMatcherTree(property.PropertyType)
                    });
                }
            }

            return nodeMatcher;
        }

        private INodeMatcher<INodeMapper> LookupNodeMatcher(Type type, Type parentLookupType, Stack<Type> recursionDetector)
        {
            // Recursion can only happen through this method, that's why we try to detect it here instead of inside TryLookupNodeMatcher
            recursionDetector.Push(parentLookupType);
            if (recursionDetector.Contains(type))
            {
                throw new YamlException($"Node matcher lookup for type '{type.FullName}' attempted to resolve itself. The recursion path was:\n{string.Join("\n", recursionDetector.Select(t => t.FullName).ToArray())}");
            }

            if (!TryLookupNodeMatcher(type, recursionDetector, out var nodeMatcher))
            {
                throw new YamlException($"Could not lookup a node matcher for type '{type.FullName}'.");
            }

            recursionDetector.Pop();
            return nodeMatcher;
        }

        private bool TryLookupNodeMatcher(Type type, Stack<Type> recursionDetector, [NotNullWhen(true)] out INodeMatcher<INodeMapper>? nodeMatcher)
        {
            foreach (var candidate in GetSuperTypes(type))
            {
                if (nodeMatchers.TryGetValue(candidate, out nodeMatcher))
                {
                    return true;
                }

                if (nodeMatcherFactories.TryGetValue(candidate, out var matcherFactory))
                {
                    nodeMatcher = matcherFactory(type, candidate, t => LookupNodeMatcher(t, type, recursionDetector));
                    return true;
                }

                if (candidate.IsGenericType() && nodeMatcherFactories.TryGetValue(candidate.GetGenericTypeDefinition(), out matcherFactory))
                {
                    nodeMatcher = matcherFactory(type, candidate, t => LookupNodeMatcher(t, type, recursionDetector));
                    return true;
                }
            }

            nodeMatcher = null;
            return false;
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

            public ObjectMapper(Type type) : this(type, GetTagName(type))
            {
            }

            public ObjectMapper(Type type, TagName tag)
            {
                this.type = type;
                Tag = tag;
            }

            private static TagName GetTagName(Type type)
            {
                var typeName = new StringBuilder(1024);
                WriteTypeName(type, typeName);
                return "!dotnet:" + Uri.EscapeUriString(typeName.ToString());
            }

            private static readonly IList<Type> EmptyTypes = new Type[0];

            private static void WriteTypeName(Type type, StringBuilder text)
            {
                var genericArguments = type.IsGenericType()
                    ? type.GetGenericArguments()
                    : EmptyTypes;

                if (type.IsGenericParameter)
                {
                }
                else if (type.IsNested)
                {
                    var parentType = type.DeclaringType!;
                    if (parentType.IsGenericTypeDefinition())
                    {
                        var nestedTypeArguments = genericArguments
                            .Zip(type.GetGenericTypeDefinition().GetGenericArguments(), (concrete, generic) => new { name = generic.Name, type = concrete });

                        genericArguments = new List<Type>();
                        var parentTypeArguments = parentType.GetGenericArguments();

                        foreach (var childTypeArgument in nestedTypeArguments)
                        {
                            var belongsToParent = false;
                            for (int i = 0; i < parentTypeArguments.Length; ++i)
                            {
                                if (parentTypeArguments[i].Name == childTypeArgument.name)
                                {
                                    belongsToParent = true;
                                    parentTypeArguments[i] = childTypeArgument.type;
                                    break;
                                }
                            }
                            if (!belongsToParent)
                            {
                                genericArguments.Add(childTypeArgument.type);
                            }
                        }

                        if (!type.IsGenericTypeDefinition())
                        {
                            parentType = parentType.MakeGenericType(parentTypeArguments);
                        }
                    }

                    WriteTypeName(parentType, text);
                    text.Append('.');
                }
                else if (!string.IsNullOrEmpty(type.Namespace))
                {
                    text.Append(type.Namespace).Append('.');
                }

                var name = type.Name;
                if (type.IsGenericType())
                {
                    text.Append(name);
                    var quoteIndex = name.IndexOf('`');
                    if (name.IndexOf('`') >= 0)
                    {
                        text.Length -= name.Length - quoteIndex; // Remove the "`1"
                    }

                    if (genericArguments.Count > 0)
                    {
                        text.Append('(');
                        foreach (var arg in genericArguments)
                        {
                            WriteTypeName(arg, text);
                            text.Append(", ");
                        }
                        text.Length -= 2; // Remove the last ", "
                        text.Append(')');
                    }
                }
                else
                {
                    text.Append(name);
                }
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
                        var value = property.GetValue(native, null);
                        // TODO: Proper null handling
                        if (value != null)
                        {
                            var key = property.Name; // TODO: Naming convention
                            var keyNode = new Scalar(StringMapper.Default, key);

                            using (currentPath.Push(keyNode))
                            {
                                var valueMapper = schema.ResolveChildMapper(value, currentPath.GetCurrentPath());
                                var valueNode = valueMapper.Represent(value, schema, currentPath);

                                children.Add(keyNode, valueNode);
                            }
                        }
                    }
                }
                return mapping;
            }

            public override string ToString()
            {
                return Tag.ToString();
            }
        }

        public bool IsTagImplicit(Scalar node, IEnumerable<INodePathSegment> path, out ScalarStyle style)
        {
            if (rootMatcher.Query(path, out var mapper))
            {
                style = ScalarStyle.Plain;
                return node.Tag.Equals(mapper.Tag);
            }

            return this.baseSchema.IsTagImplicit(node, path, out style);
        }

        public bool IsTagImplicit(Mapping node, IEnumerable<INodePathSegment> path, out MappingStyle style)
        {
            if (rootMatcher.Query(path, out var mapper))
            {
                style = MappingStyle.Block;
                return node.Tag.Equals(mapper.Tag);
            }

            return this.baseSchema.IsTagImplicit(node, path, out style);
        }

        public bool IsTagImplicit(Sequence node, IEnumerable<INodePathSegment> path, out SequenceStyle style)
        {
            if (rootMatcher.Query(path, out var mapper))
            {
                style = SequenceStyle.Block;
                return node.Tag.Equals(mapper.Tag);
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

        public INodeMapper ResolveChildMapper(object? native, IEnumerable<INodePathSegment> path)
        {
            foreach (var mapper in rootMatcher.QueryChildren(path))
            {
                // TODO: Use the native value for something ?
                return mapper;
            }

            return this.baseSchema.ResolveChildMapper(native, path);
        }

        public bool ResolveNonSpecificTag(Core.Events.Scalar node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
        {
            if (rootMatcher.Query(path, out var mapper))
            {
                // TODO: Check the node itself ?
                resolvedTag = mapper;
                return true;
            }

            return this.baseSchema.ResolveNonSpecificTag(node, path, out resolvedTag);
        }

        public bool ResolveNonSpecificTag(MappingStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
        {
            if (rootMatcher.Query(path, out var mapper))
            {
                // TODO: Check the node itself ?
                resolvedTag = mapper;
                return true;
            }

            return this.baseSchema.ResolveNonSpecificTag(node, path, out resolvedTag);
        }

        public bool ResolveNonSpecificTag(SequenceStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
        {
            if (rootMatcher.Query(path, out var mapper))
            {
                // TODO: Check the node itself ?
                resolvedTag = mapper;
                return true;
            }

            return this.baseSchema.ResolveNonSpecificTag(node, path, out resolvedTag);
        }
    }
}
