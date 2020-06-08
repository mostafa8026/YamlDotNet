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

namespace YamlDotNet.Representation.Schemas
{
    public sealed class ContextFreeSchema : ISchema
    {
        public ContextFreeSchema(IEnumerable<INodeMatcher> matchers)
        {
            Root = new Iterator(matchers);
        }

        public ISchemaIterator Root { get; }

        private sealed class Iterator : ISchemaIterator
        {
            private readonly IEnumerable<INodeMatcher> matchers;
            private readonly ILookup<TagName, INodeMatcher> matchersByTag;
            private readonly IDictionary<TagName, INodeMapper> knownTags;

            public Iterator(IEnumerable<INodeMatcher> matchers)
            {
                if (matchers is null)
                {
                    throw new ArgumentNullException(nameof(matchers));
                }

                this.matchers = matchers.ToList();
                this.matchersByTag = this.matchers.ToLookup(m => m.Mapper.Tag);

                knownTags = new Dictionary<TagName, INodeMapper>();

                foreach (var matcher in matchers)
                {
                    var canonicalMapper = matcher.Mapper.Canonical;
                    if (knownTags.TryGetValue(canonicalMapper.Tag, out var otherMapper))
                    {
                        if (!otherMapper.Equals(canonicalMapper))
                        {
                            throw new ArgumentException($"Two different mappers use tag '{canonicalMapper.Tag}'. Only one canonical mapper per tag is allowed.");
                        }
                    }
                    else
                    {
                        knownTags.Add(canonicalMapper.Tag, canonicalMapper);
                    }
                }
            }

            public ISchemaIterator EnterScalar(TagName tag, string value) => this;
            public ISchemaIterator EnterSequence(TagName tag) => this;
            public ISchemaIterator EnterMapping(TagName tag) => this;
            public ISchemaIterator EnterMappingValue() => this;

            public bool TryResolveMapper(NodeEvent node, [NotNullWhen(true)] out INodeMapper? mapper)
            {
                if (node.Tag.IsNonSpecific)
                {
                    foreach (var matcher in matchers)
                    {
                        if (matcher.Matches(node))
                        {
                            mapper = matcher.Mapper;
                            return true;
                        }
                    }
                }

                return knownTags.TryGetValue(node.Tag, out mapper);
            }

            public bool IsTagImplicit(Scalar scalar, out ScalarStyle style)
            {
                foreach (var matcher in matchersByTag[scalar.Tag])
                {
                    if (matcher.Matches(scalar))
                    {
                        // TODO: Style
                        style = default;
                        return true;
                    }
                }

                style = default;
                return true;
            }

            public bool IsTagImplicit(Sequence sequence, out SequenceStyle style)
            {
                foreach (var matcher in matchersByTag[sequence.Tag])
                {
                    if (matcher.Matches(sequence))
                    {
                        // TODO: Style
                        style = default;
                        return true;
                    }
                }

                style = default;
                return true;
            }

            public bool IsTagImplicit(Mapping mapping, out MappingStyle style)
            {
                foreach (var matcher in matchersByTag[mapping.Tag])
                {
                    if (matcher.Matches(mapping))
                    {
                        // TODO: Style
                        style = default;
                        return true;
                    }
                }

                style = default;
                return true;
            }
        }
    }
}
