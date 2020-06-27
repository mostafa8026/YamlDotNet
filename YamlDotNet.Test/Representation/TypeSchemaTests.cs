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
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.Representation;
using YamlDotNet.Representation.Schemas;
using YamlDotNet.Serialization.Schemas;

namespace YamlDotNet.Test.Representation
{
    public class TypeSchemaTests
    {
        private readonly ITestOutputHelper output;

        public TypeSchemaTests(ITestOutputHelper output)
        {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
        }

        [Fact]
        public void X()
        {
            var sut = new TypeSchema(typeof(SimpleModel), BuildTypeMatcherTable());
            output.WriteLine(sut.ToString());

            var stream = Stream.Load(Yaml.ParserForText(@"
                Value: 123
                List: [ 1, 2 ]
                Dict:
                    1: one
                    2: two
                    3: three
                RecursiveChild: { Value: 456 }
                Child: { Name: abc }
            "), _ => sut);

            var content = stream.First().Content;

            var yaml = Stream.Dump(new[] { new Document(content, new ContextFreeSchema(Enumerable.Empty<NodeMatcher>())) });
            output.WriteLine("=== Dumped YAML ===");
            output.WriteLine(yaml);


            var value = content.Mapper.Construct(content);

            var model = Assert.IsType<SimpleModel>(value);
            Assert.Equal(123, model.Value);
            Assert.Equal(456, model.RecursiveChild.Value);
            Assert.Equal("abc", model.Child.Name);
            Assert.Equal(new[] { 1, 2 }, model.List);

            Assert.Equal(3, model.Dict.Count);
            Assert.True(model.Dict.TryGetValue(1, out var one) && one == "one");
            Assert.True(model.Dict.TryGetValue(2, out var two) && two == "two");
            Assert.True(model.Dict.TryGetValue(3, out var three) && three == "three");

            var representation = sut.Represent(model);

            yaml = Stream.Dump(new[] { new Document(representation.Content, new ContextFreeSchema(Enumerable.Empty<NodeMatcher>())) });
            output.WriteLine("=== Dumped YAML ===");
            output.WriteLine(yaml);

            Assert.Equal(content, representation.Content);

            //var yaml = Stream.Dump(new[] { representation });
            //output.WriteLine("=== Dumped YAML ===");
            //output.WriteLine(yaml);

            //var yaml = Stream.Dump(new[] { new Document(content, FailsafeSchema.Strict) });
            //var yaml = Stream.Dump(new[] { new Document(content, new ContextFreeSchema(Enumerable.Empty<NodeMatcher>())) });
            //output.WriteLine("=== Dumped YAML ===");
            //output.WriteLine(yaml);
        }

        [Fact]
        public void SequenceMapperTest()
        {
            var sut = new TypeSchema(typeof(IList<int>), BuildTypeMatcherTable());
            output.WriteLine(sut.ToString());

            var doc = sut.Represent(new[] { 1, 2 });

            var yaml = Stream.Dump(new[] { new Document(doc.Content, new ContextFreeSchema(Enumerable.Empty<NodeMatcher>())) });
            output.WriteLine("=== Dumped YAML ===");
            output.WriteLine(yaml);
        }

        [Fact]
        public void SequenceMapperTest2()
        {
            var sut = new TypeSchema(typeof(IList<IList<int>>), BuildTypeMatcherTable());
            output.WriteLine(sut.ToString());

            //var stream = Stream.Load(Yaml.ParserForText(@"
            //    - 1
            //    - 2
            //"), _ => sut);

            //var content = stream.First().Content;

            //var value = content.Mapper.Construct(content);
            //var model = Assert.IsType<List<int>>(value);
            //Assert.Equal(new[] { 1, 2 }, model);

            var doc = sut.Represent(new[] { new[] { 1, 2 }, new[] { 3 } });

            var yaml = Stream.Dump(new[] { new Document(doc.Content, new ContextFreeSchema(Enumerable.Empty<NodeMatcher>())) });
            output.WriteLine("=== Dumped YAML ===");
            output.WriteLine(yaml);

            //var yaml = Stream.Dump(new[] { doc });
            //output.WriteLine(yaml);
        }

        [Fact]
        public void MappingMapperTest()
        {
            var sut = new TypeSchema(typeof(IDictionary<int, string>), BuildTypeMatcherTable());
            output.WriteLine(sut.ToString());

            var doc = sut.Represent(new Dictionary<int, string> { { 1, "one" }, { 2, "two" } });

            var yaml = Stream.Dump(new[] { new Document(doc.Content, new ContextFreeSchema(Enumerable.Empty<NodeMatcher>())) });
            output.WriteLine("=== Dumped YAML ===");
            output.WriteLine(yaml);
        }

        [Fact]
        public void ObjectMapperTest()
        {
            var model = new { one = 1, two = "abc" };

            var sut = new TypeSchema(model.GetType(), BuildTypeMatcherTable());
            output.WriteLine(sut.ToString());

            var doc = sut.Represent(model);

            var yaml = Stream.Dump(new[] { new Document(doc.Content, new ContextFreeSchema(Enumerable.Empty<NodeMatcher>())) });
            output.WriteLine("=== Dumped YAML ===");
            output.WriteLine(yaml);
        }

        private static TypeMatcherTable BuildTypeMatcherTable()
        {
            var tagNameResolver = new CompositeTagNameResolver(
                //new TableTagNameResolver(tagMappings.ToDictionary(p => p.Value, p => p.Key).AsReadonlyDictionary()),
                TypeNameTagNameResolver.Instance
            );

            var schema = CoreSchema.Instance;

            var typeMatchers = new TypeMatcherTable(false)
            {
                schema.GetNodeMatcherForTag(YamlTagRepository.Integer),
                schema.GetNodeMatcherForTag(YamlTagRepository.String),

                {
                    typeof(ICollection<>),
                    (concrete, iCollection, lookupMatcher) =>
                    {
                        if (!tagNameResolver.Resolve(concrete, out var tag))
                        {
                            throw new ArgumentException($"Could not resolve a tag for type '{concrete.FullName}'.");
                        }

                        var genericArguments = iCollection.GetGenericArguments();
                        var itemType = genericArguments[0];

                        var implementation = concrete;
                        if (concrete.IsInterface)
                        {
                            implementation = typeof(List<>).MakeGenericType(genericArguments);
                        }

                        var matcher = NodeMatcher
                            .ForSequences(SequenceMapper.Create(tag, concrete, implementation, itemType))
                            .Either(
                                s => s.MatchEmptyTags(),
                                s => s.MatchTag(tag)
                            )
                            .MatchTypes(concrete)
                            .Create();

                        return (
                            matcher,
                            () => matcher.AddItemMatcher(lookupMatcher(itemType))
                        );
                    }
                },
                {
                    typeof(IDictionary<,>),
                    (concrete, iDictionary, lookupMatcher) =>
                    {
                        if (!tagNameResolver.Resolve(concrete, out var tag))
                        {
                            throw new ArgumentException($"Could not resolve a tag for type '{concrete.FullName}'.");
                        }

                        var genericArguments = iDictionary.GetGenericArguments();
                        var keyType = genericArguments[0];
                        var valueType = genericArguments[1];

                        var implementation = concrete;
                        if (concrete.IsInterface)
                        {
                            implementation = typeof(Dictionary<,>).MakeGenericType(genericArguments);
                        }

                        var matcher = NodeMatcher
                            .ForMappings(MappingMapper.Create(tag, concrete, implementation, keyType, valueType))
                            .Either(
                                s => s.MatchEmptyTags(),
                                s => s.MatchTag(tag)
                            )
                            .MatchTypes(concrete)
                            .Create();

                        return (
                            matcher,
                            () =>
                            {
                                matcher.AddItemMatcher(
                                    keyMatcher: lookupMatcher(keyType),
                                    valueMatchers: lookupMatcher(valueType)
                                );
                            }
                        );
                    }
                },
                {
                    typeof(object),
                    (concrete, _, lookupMatcher) =>
                    {
                        if (!tagNameResolver.Resolve(concrete, out var tag))
                        {
                            throw new ArgumentException($"Could not resolve a tag for type '{concrete.FullName}'.");
                        }
                        var mapper = new ObjectMapper(concrete, tag);

                        var matcher = NodeMatcher
                            .ForMappings(mapper)
                            .Either(
                                s => s.MatchEmptyTags(),
                                s => s.MatchTag(tag)
                            )
                            .MatchTypes(concrete)
                            .Create();

                        return (
                            matcher,
                            () =>
                            {
                                // TODO: Type inspector
                                var properties = concrete.GetPublicProperties();
                                foreach (var property in properties)
                                {
                                    matcher.AddItemMatcher(
                                        keyMatcher: NodeMatcher
                                            .ForScalars(StringMapper.Default) // TODO: Naming convention
                                            .MatchValue(property.Name) // TODO: Naming convention
                                            .Create(),
                                        valueMatchers: lookupMatcher(property.PropertyType)
                                    );
                                }
                            }
                        );
                    }
                }
            };
            return typeMatchers;
        }

#nullable disable

        public class SimpleModel
        {
            public int Value { get; set; }

            public List<int> List { get; set; }
            public Dictionary<int, string> Dict { get; set; }

            //// TODO: List<SimpleModelChild>

            public SimpleModel RecursiveChild { get; set; }
            public SimpleModelChild Child { get; set; }
        }

        public class SimpleModelChild
        {
            public string Name { get; set; }
        }
    }
}
