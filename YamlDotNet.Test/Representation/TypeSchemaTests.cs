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
using YamlDotNet.Helpers;
using YamlDotNet.Representation;
using YamlDotNet.Representation.Schemas;

namespace YamlDotNet.Test.Representation
{
    public class TypeSchemaTests
    {
        private readonly ITestOutputHelper output;

        public TypeSchemaTests(ITestOutputHelper output)
        {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
        }

        private INodeMapper GetCoreMapper(TagName tag)
        {
            return CoreSchema.Instance.ResolveMapper(tag, out var mapper)
                ? mapper
                : throw new Exception($"Mapper for tag '{tag}' not found.");
        }

        [Fact]
        public void X()
        {
            var tagMappings = new Dictionary<Type, Either<INodeMatcher<INodeMapper>, NodeMatcherFactory>>
            {
                {
                    typeof(int),
                    new NodeKindMatcher<INodeMapper>(NodeKind.Scalar, GetCoreMapper(YamlTagRepository.Integer))
                },
                {
                    typeof(string),
                    new NodeKindMatcher<INodeMapper>(NodeKind.Scalar, GetCoreMapper(YamlTagRepository.String))
                },
                {
                    typeof(List<int>),
                    new NodeKindMatcher<INodeMapper>(NodeKind.Sequence, SequenceMapper<int>.Default)
                    {
                        new NodeKindMatcher<INodeMapper>(NodeKind.Scalar, GetCoreMapper(YamlTagRepository.Integer))
                    }
                },
                {
                    typeof(Dictionary<int, string>),
                    new NodeKindMatcher<INodeMapper>(NodeKind.Mapping, MappingMapper<int, string>.Default)
                    {
                        new NodeKindMatcher<INodeMapper>(NodeKind.Scalar, GetCoreMapper(YamlTagRepository.Integer))
                        {
                            new NodeKindMatcher<INodeMapper>(NodeKind.Scalar, GetCoreMapper(YamlTagRepository.String))
                        }
                    }
                },
            };

            var sut = new TypeSchema(typeof(SimpleModel), CoreSchema.Instance, tagMappings);

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
            var value = content.Mapper.Construct(content);

            var model = Assert.IsType<SimpleModel>(value);
            Assert.Equal(123, model.Value);
            Assert.Equal(456, model.RecursiveChild!.Value);

            var representation = sut.Represent(model);
            Assert.Equal(content, representation.Content);

            var yaml = Stream.Dump(new[] { representation });
            output.WriteLine("=== Dumped YAML ===");
            output.WriteLine(yaml);

            yaml = Stream.Dump(new[] { new Document(representation.Content, FailsafeSchema.Strict) });
            output.WriteLine("=== Dumped YAML ===");
            output.WriteLine(yaml);
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
