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
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Helpers;
using YamlDotNet.Representation.Schemas;
using Events = YamlDotNet.Core.Events;

namespace YamlDotNet.Representation
{
    public sealed class Stream
    {
        public static IEnumerable<Document> Load(string yaml, Func<DocumentStart, ISchema> schemaSelector)
        {
            using var reader = new StringReader(yaml);
            return Load(new Parser(reader), schemaSelector);
        }

        public static IEnumerable<Document> Load(IParser parser, Func<DocumentStart, ISchema> schemaSelector)
        {
            static IEnumerable<Document> LoadDocuments(IParser parser, Func<DocumentStart, ISchema> schemaSelector)
            {
                parser.Consume<StreamStart>();
                while (parser.TryConsume<DocumentStart>(out var documentStart))
                {
                    var schema = schemaSelector(documentStart);
                    var loader = new NodeLoader(parser, schema);
                    var content = parser.Consume<NodeEvent>();
                    var node = content.Accept(loader);
                    parser.Consume<DocumentEnd>();
                    yield return new Document(node, schema);
                }
                parser.Consume<StreamEnd>();
            }

            return LoadDocuments(parser, schemaSelector).SingleUse();
        }

        public static string Dump(IEnumerable<Document> stream, bool explicitSeparators = false)
        {
            using var buffer = new StringWriter();
            Dump(new Emitter(buffer), stream, explicitSeparators);
            return buffer.ToString();
        }

        public static void Dump(IEmitter emitter, IEnumerable<Document> stream, bool explicitSeparators = false)
        {
            emitter.Emit(new StreamStart());
            foreach (var document in stream)
            {
                emitter.Emit(new DocumentStart(null, null, isImplicit: !explicitSeparators));

                var count = 0;
                var anchorAssigner = new AnchorAssigner(_ => $"n{count++}");
                document.Content.Accept(anchorAssigner);

                var dumper = new NodeDumper(emitter, document.Schema, anchorAssigner.GetAssignedAnchors());
                document.Content.Accept(dumper);

                emitter.Emit(new DocumentEnd(isImplicit: !explicitSeparators));
            }
            emitter.Emit(new StreamEnd());
        }

        private sealed class NodeLoader : IParsingEventVisitor<Node>
        {
            private readonly Dictionary<AnchorName, Node> anchoredNodes = new Dictionary<AnchorName, Node>();
            private readonly NodePath currentPath = new NodePath();
            private readonly IParser parser;
            private readonly ISchema schema;

            public NodeLoader(IParser parser, ISchema schema)
            {
                this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
                this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            }

            public Node Visit(AnchorAlias anchorAlias)
            {
                if (anchoredNodes.TryGetValue(anchorAlias.Value, out var node))
                {
                    return node;
                }
                throw new AnchorNotFoundException(anchorAlias.Start, anchorAlias.End, $"Anchor '{anchorAlias.Value}' not found.");
            }

            private Exception UnexpectedEvent(ParsingEvent parsingEvent)
            {
                return new SemanticErrorException(parsingEvent.Start, parsingEvent.End, $"Found an unexpected event '{parsingEvent.Type}'.");
            }

            public Node Visit(Events.Scalar scalar)
            {
                using (currentPath.Push(scalar))
                {
                    var mapper = ResolveNode(scalar, schema.ResolveNonSpecificTag);
                    return new Scalar(mapper, scalar.Value);
                }
            }

            public Node Visit(SequenceStart sequenceStart)
            {
                using (currentPath.Push(sequenceStart))
                {
                    var mapper = ResolveNode(sequenceStart, schema.ResolveNonSpecificTag);

                    var items = new List<Node>();
                    while (parser.TryConsume<NodeEvent>(out var nodeEvent))
                    {
                        var child = nodeEvent.Accept(this);
                        items.Add(child);
                    }
                    parser.Consume<SequenceEnd>();
                    return new Sequence(mapper, items.AsReadonlyList());
                }
            }

            public Node Visit(MappingStart mappingStart)
            {
                using (currentPath.Push(mappingStart))
                {
                    var mapper = ResolveNode(mappingStart, schema.ResolveNonSpecificTag);
                    var items = new Dictionary<Node, Node>();
                    while (parser.TryConsume<NodeEvent>(out var keyNodeEvent))
                    {
                        var key = keyNodeEvent.Accept(this);

                        var valueNodeEvent = parser.Consume<NodeEvent>();
                        using (currentPath.Push(keyNodeEvent))
                        {
                            var value = valueNodeEvent.Accept(this);
                            items.Add(key, value);
                        }
                    }
                    parser.Consume<MappingEnd>();
                    return new Mapping(mapper, items.AsReadonlyDictionary());
                }
            }

            public Node Visit(Comment comment) => throw UnexpectedEvent(comment);
            public Node Visit(SequenceEnd sequenceEnd) => throw UnexpectedEvent(sequenceEnd);
            public Node Visit(MappingEnd mappingEnd) => throw UnexpectedEvent(mappingEnd);
            public Node Visit(DocumentStart documentStart) => throw UnexpectedEvent(documentStart);
            public Node Visit(DocumentEnd documentEnd) => throw UnexpectedEvent(documentEnd);
            public Node Visit(StreamStart streamStart) => throw UnexpectedEvent(streamStart);
            public Node Visit(StreamEnd streamEnd) => throw UnexpectedEvent(streamEnd);

            private delegate bool ResolveNonSpecificTagDelegate<TNode>(TNode node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag);

            private INodeMapper ResolveNode<TNode>(TNode node, ResolveNonSpecificTagDelegate<TNode> resolveNonSpecificTag)
                where TNode : NodeEvent
            {
                var path = currentPath.GetCurrentPath();
                if (node.Tag.IsNonSpecific)
                {
                    if (resolveNonSpecificTag(node, path, out var resolvedTag))
                    {
                        return resolvedTag;
                    }
                }
                else if (schema.ResolveMapper(node.Tag, out var resolvedTag))
                {
                    return resolvedTag;
                }

                return new UnknownTagMapper(node.Tag, ((INodePathSegment)node).Kind);
            }

            private sealed class UnknownTagMapper : INodeMapper
            {
                public TagName Tag { get; }
                public NodeKind MappedNodeKind { get; }

                public UnknownTagMapper(TagName tag, NodeKind mappedNodeKind)
                {
                    Tag = tag;
                    MappedNodeKind = mappedNodeKind;
                }

                public object? Construct(Node node)
                {
                    throw new NotSupportedException($"The tag '{Tag}' was not recognized by the current schema.");
                }

                public Node Represent(object? native, ISchema schema, NodePath currentPath)
                {
                    throw new NotSupportedException($"The tag '{Tag}' was not recognized by the current schema.");
                }
            }
        }

        private sealed class AnchorAssigner : INodeVisitor<Empty>
        {
            private readonly Func<Node, AnchorName> assignAnchor;
            private readonly HashSet<Node> encounteredNodes = new HashSet<Node>(ReferenceEqualityComparer<Node>.Default);
            private readonly Dictionary<Node, AnchorName> assignedAnchors = new Dictionary<Node, AnchorName>(ReferenceEqualityComparer<Node>.Default);

            public AnchorAssigner(Func<Node, AnchorName> assignAnchor)
            {
                this.assignAnchor = assignAnchor ?? throw new ArgumentNullException(nameof(assignAnchor));
            }

            public Dictionary<Node, AnchorName> GetAssignedAnchors()
            {
                return assignedAnchors;
            }

            private Empty VisitNode(Node node)
            {
                if (!encounteredNodes.Add(node))
                {
                    assignedAnchors.Add(node, assignAnchor(node));
                }
                return default;
            }

            Empty INodeVisitor<Empty>.Visit(Scalar scalar) => VisitNode(scalar);
            Empty INodeVisitor<Empty>.Visit(Sequence sequence) => VisitNode(sequence);
            Empty INodeVisitor<Empty>.Visit(Mapping mapping) => VisitNode(mapping);
        }

        private sealed class NodeDumper : INodeVisitor<Empty>
        {
            private readonly NodePath currentPath = new NodePath();
            private readonly IEmitter emitter;
            private readonly ISchema schema;
            private readonly Dictionary<Node, AnchorName> anchors;
            private readonly HashSet<Node> emittedAnchoredNodes = new HashSet<Node>(ReferenceEqualityComparer<Node>.Default);

            public NodeDumper(IEmitter emitter, ISchema schema, Dictionary<Node, AnchorName> anchors)
            {
                this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
                this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
                this.anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
            }

            public Empty Visit(Scalar scalar)
            {
                if (!TryEmitAlias(scalar, out var anchor))
                {
                    using (currentPath.Push(scalar))
                    {
                        var path = currentPath.GetCurrentPath();
                        var tag = schema.IsTagImplicit(scalar, path, out var style) ? TagName.Empty : scalar.Tag;

                        emitter.Emit(new Events.Scalar(anchor, tag, scalar.Value, style));
                    }
                }
                return default;
            }

            public Empty Visit(Sequence sequence)
            {
                if (!TryEmitAlias(sequence, out var anchor))
                {
                    using (currentPath.Push(sequence))
                    {
                        var path = currentPath.GetCurrentPath();
                        var tag = schema.IsTagImplicit(sequence, path, out var style) ? TagName.Empty : sequence.Tag;

                        var sequenceStart = new SequenceStart(anchor, tag, style);
                        emitter.Emit(sequenceStart);

                        foreach (var item in sequence)
                        {
                            item.Accept(this);
                        }
                    }

                    emitter.Emit(new SequenceEnd());
                }
                return default;
            }

            public Empty Visit(Mapping mapping)
            {
                if (!TryEmitAlias(mapping, out var anchor))
                {
                    using (currentPath.Push(mapping))
                    {
                        var path = currentPath.GetCurrentPath();
                        var tag = schema.IsTagImplicit(mapping, path, out var style) ? TagName.Empty : mapping.Tag;

                        var mappingStart = new MappingStart(anchor, tag, style);
                        emitter.Emit(mappingStart);

                        foreach (var (key, value) in mapping)
                        {
                            key.Accept(this);
                            using (currentPath.Push(key))
                            {
                                value.Accept(this);
                            }
                        }
                    }

                    emitter.Emit(new MappingEnd());
                }
                return default;
            }

            private bool TryEmitAlias(Node node, out AnchorName anchor)
            {
                if (anchors.TryGetValue(node, out anchor) && !emittedAnchoredNodes.Add(node))
                {
                    emitter.Emit(new AnchorAlias(anchor));
                    return true;
                }
                return false;
            }
        }
    }
}