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
using YamlDotNet.Core.Events;
using YamlDotNet.Core.Schemas;
using ParsingEvent = YamlDotNet.Core.Events.ParsingEvent;

namespace YamlDotNet.Core
{
    public sealed class SchemaAwareEmitter : IEmitter
    {
        private readonly IEmitter emitter;
        private readonly ImplicitTagResolver implicitTagResolver;

        public SchemaAwareEmitter(IEmitter emitter, ISchema schema)
        {
            this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
            this.implicitTagResolver = new ImplicitTagResolver(schema);
        }

        private sealed class ImplicitTagResolver : IParsingEventVisitor<ParsingEvent>
        {
            private readonly ParsingEventPathBuilder currentPath = new ParsingEventPathBuilder();
            private readonly ISchema schema;

            public ImplicitTagResolver(ISchema schema)
            {
                this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            }

            ParsingEvent IParsingEventVisitor<ParsingEvent>.Visit(Scalar scalar)
            {
                var path = scalar.Accept(currentPath);
                if (schema.IsTagImplicit(scalar, path))
                {
                    scalar = new Scalar(scalar.Anchor, SimpleTag.NonSpecificOtherNodes, scalar.Value, scalar.Style, scalar.Start, scalar.End);
                }
                return scalar;
            }

            ParsingEvent IParsingEventVisitor<ParsingEvent>.Visit(SequenceStart sequenceStart)
            {
                var path = sequenceStart.Accept(currentPath);
                if (schema.IsTagImplicit(sequenceStart, path))
                {
                    sequenceStart = new SequenceStart(sequenceStart.Anchor, SimpleTag.NonSpecificOtherNodes, sequenceStart.Style, sequenceStart.Start, sequenceStart.End);
                }
                return sequenceStart;
            }

            ParsingEvent IParsingEventVisitor<ParsingEvent>.Visit(MappingStart mappingStart)
            {
                var path = mappingStart.Accept(currentPath);
                if (schema.IsTagImplicit(mappingStart, path))
                {
                    mappingStart = new MappingStart(mappingStart.Anchor, SimpleTag.NonSpecificOtherNodes, mappingStart.Style, mappingStart.Start, mappingStart.End);
                }
                return mappingStart;
            }

            ParsingEvent IParsingEventVisitor<ParsingEvent>.Visit(SequenceEnd sequenceEnd)
            {
                sequenceEnd.Accept(currentPath);
                return sequenceEnd;
            }

            ParsingEvent IParsingEventVisitor<ParsingEvent>.Visit(MappingEnd mappingEnd)
            {
                mappingEnd.Accept(currentPath);
                return mappingEnd;
            }


            ParsingEvent IParsingEventVisitor<ParsingEvent>.Visit(AnchorAlias anchorAlias) => anchorAlias;
            ParsingEvent IParsingEventVisitor<ParsingEvent>.Visit(StreamStart streamStart) => streamStart;
            ParsingEvent IParsingEventVisitor<ParsingEvent>.Visit(StreamEnd streamEnd) => streamEnd;
            ParsingEvent IParsingEventVisitor<ParsingEvent>.Visit(DocumentStart documentStart) => documentStart;
            ParsingEvent IParsingEventVisitor<ParsingEvent>.Visit(DocumentEnd documentEnd) => documentEnd;
            ParsingEvent IParsingEventVisitor<ParsingEvent>.Visit(Comment comment) => comment;
        }

        public void Emit(ParsingEvent @event)
        {
            var resolvedEvent = @event.Accept(implicitTagResolver);
            this.emitter.Emit(resolvedEvent);
        }
    }
}
