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
using YamlDotNet.Core;
using YamlDotNet.Helpers;
using YamlDotNet.Representation;
using YamlDotNet.Representation.Schemas;
using YamlDotNet.Serialization.Utilities;
using Scalar = YamlDotNet.Representation.Scalar;

namespace YamlDotNet.Serialization.Schemas
{
    public sealed class ObjectMapper : INodeMapper
    {
        private readonly Type type;

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
}
