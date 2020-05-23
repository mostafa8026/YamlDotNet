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
using System.Reflection;
using YamlDotNet.Core;
using YamlDotNet.Helpers;
using YamlDotNet.Serialization.Utilities;

namespace YamlDotNet.Representation.Schemas
{
    /// <summary>
    /// The tag:yaml.org,2002:map tag, as specified in the Failsafe schema.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Dictionary{TKey, TValue}(System.Object, System.Object)" /> as native representation.
    /// </remarks>
    public sealed class MappingMapper : INodeMapper
    {
        public static readonly MappingMapper Instance = new MappingMapper();

        private MappingMapper() { }

        public TagName Tag => YamlTagRepository.Mapping;
        public NodeKind MappedNodeKind => NodeKind.Mapping;

        public object? Construct(Node node)
        {
            var mapping = node.Expect<Mapping>();
            var dictionary = new Dictionary<object, object?>(mapping.Count);
            foreach (var (keyNode, valueNode) in mapping)
            {
                var key = keyNode.Mapper.Construct(keyNode)!;
                var value = valueNode.Mapper.Construct(valueNode);
                dictionary.Add(key, value);
            }
            return dictionary;
        }

        public Node Represent(object? native, ISchema schema, NodePath currentPath)
        {
            var items = new Dictionary<Node, Node>();

            // Notice that the items collection will still be mutated after constructing the Sequence object.
            // We need to create it now in order to update the current path.
            var mapping = new Mapping(this, items.AsReadonlyDictionary());

            using (currentPath.Push(mapping))
            {
                var basePath = currentPath.GetCurrentPath();
                foreach (var (key, value) in IterateDictonary(native!))
                {
                    var keyMapper = schema.ResolveMapper(key, basePath);
                    var keyNode = keyMapper.Represent(key, schema, currentPath);

                    using (currentPath.Push(keyNode))
                    {
                        var valueMapper = schema.ResolveMapper(value, basePath);
                        var valueNode = valueMapper.Represent(value, schema, currentPath);

                        items.Add(keyNode, valueNode);
                    }
                }
            }

            return mapping;
        }

        private static IEnumerable<KeyValuePair<object, object?>> IterateDictonary(object dictionary)
        {
            if (dictionary is IDictionary nonGenericDictionary)
            {
                return IterateNonGenericDictionary(nonGenericDictionary);
            }
            else
            {
                var iDictionaryType = ReflectionUtility.GetImplementedGenericInterface(typeof(IDictionary<,>), dictionary.GetType());
                if (iDictionaryType == null)
                {
                    throw new InvalidOperationException("The object must implement either IDictionary or IDictionary<TKey, TValue>.");
                }

                return (IEnumerable<KeyValuePair<object, object?>>)iterateGenericDictonaryMethod
                    .MakeGenericMethod(iDictionaryType.GetGenericArguments())
                    .Invoke(null, new object?[] { dictionary })!;
            }
        }

        private static readonly MethodInfo iterateGenericDictonaryMethod = typeof(MappingMapper).GetPrivateStaticMethod(nameof(IterateGenericDictonary));

        private static IEnumerable<KeyValuePair<object, object?>> IterateNonGenericDictionary(IDictionary nonGenericDictionary)
        {
            foreach (var entry in nonGenericDictionary)
            {
                var pair = (DictionaryEntry)entry!;
                yield return new KeyValuePair<object, object?>(pair.Key, pair.Value);
            }
        }

        private static IEnumerable<KeyValuePair<object, object?>> IterateGenericDictonary<TKey, TValue>(IDictionary<TKey, TValue> genericDictionary)
            where TKey : notnull
        {
            foreach (var entry in genericDictionary)
            {
                yield return new KeyValuePair<object, object?>(entry.Key, entry.Value);
            }
        }
    }
}
