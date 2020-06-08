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
using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace YamlDotNet.Representation.Schemas
{
    public sealed class CompositeSchema : ISchema
    {
        private readonly ISchema primary;
        private readonly ISchema secondary;

        public CompositeSchema(ISchema primary, ISchema secondary)
        {
            this.primary = primary ?? throw new ArgumentNullException(nameof(primary));
            this.secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));
        }

        public ISchemaIterator Root => new CompositeIterator(primary.Root, secondary.Root);

        private sealed class CompositeIterator : ISchemaIterator
        {
            private readonly ISchemaIterator primary;
            private readonly ISchemaIterator secondary;

            public CompositeIterator(ISchemaIterator primary, ISchemaIterator secondary)
            {
                this.primary = primary ?? throw new ArgumentNullException(nameof(primary));
                this.secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));
            }

            public ISchemaIterator EnterMapping(TagName tag)
            {
                return new CompositeIterator(primary.EnterMapping(tag), secondary.EnterMapping(tag));
            }

            public ISchemaIterator EnterMappingValue()
            {
                return new CompositeIterator(primary.EnterMappingValue(), secondary.EnterMappingValue());
            }

            public ISchemaIterator EnterScalar(TagName tag, string value)
            {
                return new CompositeIterator(primary.EnterScalar(tag, value), secondary.EnterScalar(tag, value));
            }

            public ISchemaIterator EnterSequence(TagName tag)
            {
                return new CompositeIterator(primary.EnterSequence(tag), secondary.EnterSequence(tag));
            }

            public bool TryResolveMapper(NodeEvent node, [NotNullWhen(true)] out INodeMapper? mapper)
            {
                return primary.TryResolveMapper(node, out mapper)
                    || secondary.TryResolveMapper(node, out mapper);
            }

            public bool IsTagImplicit(Scalar scalar, out ScalarStyle style)
            {
                return primary.IsTagImplicit(scalar, out style)
                    || secondary.IsTagImplicit(scalar, out style);
            }

            public bool IsTagImplicit(Sequence sequence, out SequenceStyle style)
            {
                return primary.IsTagImplicit(sequence, out style)
                    || secondary.IsTagImplicit(sequence, out style);
            }

            public bool IsTagImplicit(Mapping mapping, out MappingStyle style)
            {
                return primary.IsTagImplicit(mapping, out style)
                    || secondary.IsTagImplicit(mapping, out style);
            }
        }
    }
}
