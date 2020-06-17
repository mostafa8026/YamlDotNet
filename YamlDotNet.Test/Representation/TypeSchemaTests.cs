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
            var tagNameResolver = new CompositeTagNameResolver(
                //new TableTagNameResolver(tagMappings.ToDictionary(p => p.Value, p => p.Key).AsReadonlyDictionary()),
                TypeNameTagNameResolver.Instance
            );

            var typeMatchers = new TypeMatcherTable(false)
            {
                //    {
                //        typeof(int),
                //        new NodeKindMatcher(NodeKind.Scalar, CoreSchema.IntegerMapper.Canonical)
                //    },
                //    {
                //        typeof(string),
                //        new NodeKindMatcher(NodeKind.Scalar, GetCoreMapper(YamlTagRepository.String))
                //    },
                //    {
                //        typeof(ICollection<>),
                //        (concrete, iCollection, lookupMatcher) => {
                //            var itemType = iCollection.GetGenericArguments()[0];
                //            return new NodeKindMatcher(NodeKind.Sequence, SequenceMapper.Default(itemType))
                //            {
                //                lookupMatcher(itemType)
                //            };
                //        }
                //    },
                //    {
                //        typeof(IDictionary<,>),
                //        (concrete, iDictionary, lookupMatcher) => {
                //            var types = iDictionary.GetGenericArguments();
                //            var keyType = types[0];
                //            var valueType = types[1];

                //            var keyMapper = lookupMatcher(keyType).Value;

                //            return new NodeKindMatcher(NodeKind.Mapping, MappingMapper.Default(keyType, valueType))
                //            {
                //                new NodeKindMatcher(keyMapper.MappedNodeKind, keyMapper)
                //                {
                //                    lookupMatcher(valueType)
                //                }
                //            };
                //        }
                //    },


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
                            // TODO: Children
                            .Create();

                        // TODO: Type inspector
                        var properties = concrete.GetPublicProperties();
                        foreach (var property in properties)
                        {
                            matcher.Add(NodeMatcher
                                .ForScalars(StringMapper.Default) // TODO: Naming convention
                                .MatchValue(property.Name) // TODO: Naming convention
                                .Create()
                            );
                        }

                        return matcher;
                    }
                }
            };

            var sut = new TypeSchema(typeof(SimpleModel), CoreSchema.Instance, typeMatchers);
            output.WriteLine(sut.ToString());

            var stream = Stream.Load(Yaml.ParserForText(@"
                Value: 123
                #List: [ 1, 2 ]
                #Dict:
                #    1: one
                #    2: two
                #    3: three
                #RecursiveChild: { Value: 456 }
                #Child: { Name: abc }
            "), _ => sut);

            var content = stream.First().Content;

            var yaml = Stream.Dump(new[] { new Document(content, new ContextFreeSchema(Enumerable.Empty<NodeMatcher>())) });
            output.WriteLine("=== Dumped YAML ===");
            output.WriteLine(yaml);


            var value = content.Mapper.Construct(content);

            var model = Assert.IsType<SimpleModel>(value);
            Assert.Equal(123, model.Value);
            //Assert.Equal(456, model.RecursiveChild!.Value);

            //var representation = sut.Represent(model);
            //Assert.Equal(content, representation.Content);

            //var yaml = Stream.Dump(new[] { representation });
            //output.WriteLine("=== Dumped YAML ===");
            //output.WriteLine(yaml);

            //var yaml = Stream.Dump(new[] { new Document(content, FailsafeSchema.Strict) });
            //var yaml = Stream.Dump(new[] { new Document(content, new ContextFreeSchema(Enumerable.Empty<NodeMatcher>())) });
            //output.WriteLine("=== Dumped YAML ===");
            //output.WriteLine(yaml);
        }

        public class SimpleModel
        {
            public int Value { get; set; }

            public List<int>? List { get; set; }
            public Dictionary<int, string>? Dict { get; set; }

            // TODO: List<SimpleModelChild>

            public SimpleModel? RecursiveChild { get; set; }
            public SimpleModelChild? Child { get; set; }
        }

        public class SimpleModelChild
        {
            public string? Name { get; set; }
        }
    }
}
