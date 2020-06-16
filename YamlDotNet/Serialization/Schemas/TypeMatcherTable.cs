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
using YamlDotNet.Core;
using YamlDotNet.Helpers;
using YamlDotNet.Representation;
using YamlDotNet.Representation.Schemas;

namespace YamlDotNet.Serialization.Schemas
{
    public delegate NodeMatcher NodeMatcherFactory(Type sourceType, Type matchedType, Func<Type, NodeMatcher> nodeMapperLookup);

    public sealed class TypeMatcherTable : IEnumerable
    {
        private readonly ICache<Type, NodeMatcher> nodeMatchersByType;
        private readonly ICache<TagName, INodeMapper> nodeMappersByTag;
        private readonly IDictionary<Type, NodeMatcherFactory> nodeMatcherFactories = new Dictionary<Type, NodeMatcherFactory>();

        public TypeMatcherTable(bool requireThreadSafety)
        {
            if (requireThreadSafety)
            {
                nodeMatchersByType = new ThreadSafeCache<Type, NodeMatcher>();
                nodeMappersByTag = new ThreadSafeCache<TagName, INodeMapper>();
            }
            else
            {
                nodeMatchersByType = new SingleThreadCache<Type, NodeMatcher>();
                nodeMappersByTag = new SingleThreadCache<TagName, INodeMapper>();
            }
        }

        public void Add(Type type, INodeMapper nodeMapper)
        {
            throw new NotImplementedException("TODO");
            //Add(type, new NodeKindMatcher(nodeMapper));
        }

        public void Add(Type type, NodeMatcher nodeMatcher)
        {
            nodeMatchersByType.Add(type, nodeMatcher);
            nodeMappersByTag.GetOrAdd(nodeMatcher.Mapper.Tag, () => nodeMatcher.Mapper);
        }

        public void Add(Type type, NodeMatcherFactory nodeMatcherFactory)
        {
            nodeMatcherFactories.Add(type, nodeMatcherFactory);
        }

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException("This class implements IEnumerable only to allow collection initialization.");

        public NodeMatcher GetNodeMatcher(Type type)
        {
            if (type.IsGenericParameter)
            {
                throw new ArgumentException("Cannot get a node matcher for a generic parameter.", nameof(type));
            }

            return nodeMatchersByType.GetOrAdd(type, () =>
            {
                foreach (var candidate in GetSuperTypes(type))
                {
                    if (candidate != type) // A type cannot be resolved by itself
                    {
                        if (nodeMatchersByType.TryGetValue(candidate, out var nodeMatcher))
                        {
                            return nodeMatcher;
                        }
                    }

                    if (nodeMatcherFactories.TryGetValue(candidate, out var concreteMatcherFactory))
                    {
                        var nodeMatcher = concreteMatcherFactory(type, candidate, GetNodeMatcher);
                        nodeMappersByTag.GetOrAdd(nodeMatcher.Mapper.Tag, () => nodeMatcher.Mapper);
                        return nodeMatcher;
                    }

                    if (candidate.IsGenericType() && nodeMatcherFactories.TryGetValue(candidate.GetGenericTypeDefinition(), out var genericMatcherFactory))
                    {
                        var nodeMatcher = genericMatcherFactory(type, candidate, GetNodeMatcher);
                        nodeMappersByTag.GetOrAdd(nodeMatcher.Mapper.Tag, () => nodeMatcher.Mapper);
                        return nodeMatcher;
                    }
                }

                throw new ArgumentException($"Could not resolve a tag for type '{type.FullName}'.");
            });
        }

        public bool TryGetNodeMapper(TagName tag, [NotNullWhen(true)] out INodeMapper? nodeMapper)
        {
            return nodeMappersByTag.TryGetValue(tag, out nodeMapper);
        }

        /// <summary>
        /// Returns type and all its parent classes and implemented interfaces in this order:
        /// 1. the type itself;
        /// 2. its superclasses, starting on the base class, except object;
        /// 3. all interfaces implemented by the type;
        /// 4. typeof(object).
        /// </summary>
        private static IEnumerable<Type> GetSuperTypes(Type type)
        {
            if (type.IsInterface())
            {
                yield return type;
            }
            else
            {
                Type? ancestor = type;
                // Object will be returned last
                while (ancestor != null && ancestor != typeof(object))
                {
                    yield return ancestor;
                    ancestor = ancestor.BaseType();
                }
            }
            foreach (var itf in type.GetInterfaces())
            {
                yield return itf;
            }
            yield return typeof(object);
        }
    }
}
