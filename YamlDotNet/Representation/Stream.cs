using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Helpers;
using YamlDotNet.Representation.Schemas;
using Events = YamlDotNet.Core.Events;

namespace YamlDotNet.Representation
{
    public sealed class Stream
    {
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
                var mapper = ResolveScalar(scalar);
                return new Scalar(mapper, scalar.Value);
            }

            private IScalarMapper ResolveScalar(Events.Scalar scalar)
            {
                var path = currentPath.GetCurrentPath();
                if (scalar.Tag.IsNonSpecific)
                {
                    if (schema.ResolveNonSpecificTag(scalar, path, out var resolvedTag))
                    {
                        return resolvedTag;
                    }
                }
                else if (schema.ResolveScalarMapper(scalar.Tag, out var resolvedTag))
                {
                    return resolvedTag;
                }

                // TODO: Maybe use a different type of tag
                return new ScalarMapper(
                    scalar.Tag,
                    _ => throw new NotImplementedException(),
                    (_, __) => throw new NotImplementedException()
                );
            }

            public Node Visit(SequenceStart sequenceStart)
            {
                var tag = sequenceStart.Tag;
                if (tag.IsNonSpecific)
                {
                    var path = currentPath.GetCurrentPath();
                    if (schema.ResolveNonSpecificTag(sequenceStart, path, out var resolvedTag))
                    {
                        tag = resolvedTag;
                    }
                }

                currentPath.Push(sequenceStart);

                var items = new List<Node>();
                while (parser.TryConsume<NodeEvent>(out var nodeEvent))
                {
                    var child = nodeEvent.Accept(this);
                    items.Add(child);
                }
                parser.Consume<SequenceEnd>();

                currentPath.Pop();

                return new Sequence(tag, items.AsReadonly());
            }

            public Node Visit(MappingStart mappingStart)
            {
                var tag = mappingStart.Tag;
                if (tag.IsNonSpecific)
                {
                    var path = currentPath.GetCurrentPath();
                    if (schema.ResolveNonSpecificTag(mappingStart, path, out var resolvedTag))
                    {
                        tag = resolvedTag;
                    }
                }

                currentPath.Push(mappingStart);

                var items = new Dictionary<Node, Node>();
                while (parser.TryConsume<NodeEvent>(out var keyNodeEvent))
                {
                    var key = keyNodeEvent.Accept(this);

                    var valueNodeEvent = parser.Consume<NodeEvent>();
                    var value = valueNodeEvent.Accept(this);

                    items.Add(key, value);
                }
                parser.Consume<MappingEnd>();

                currentPath.Pop();

                return new Mapping(tag, items.AsReadonly());
            }

            public Node Visit(Comment comment) => throw UnexpectedEvent(comment);
            public Node Visit(SequenceEnd sequenceEnd) => throw UnexpectedEvent(sequenceEnd);
            public Node Visit(MappingEnd mappingEnd) => throw UnexpectedEvent(mappingEnd);
            public Node Visit(DocumentStart documentStart) => throw UnexpectedEvent(documentStart);
            public Node Visit(DocumentEnd documentEnd) => throw UnexpectedEvent(documentEnd);
            public Node Visit(StreamStart streamStart) => throw UnexpectedEvent(streamStart);
            public Node Visit(StreamEnd streamEnd) => throw UnexpectedEvent(streamEnd);
        }

        private sealed class AnchorAssigner : INodeVisitor<Empty>
        {
            private readonly Func<Node, AnchorName> assignAnchor;
            private readonly HashSet<Node> encounteredNodes = new HashSet<Node>(ReferenceEqualityComparer<Node>.Instance);
            private readonly Dictionary<Node, AnchorName> assignedAnchors = new Dictionary<Node, AnchorName>(ReferenceEqualityComparer<Node>.Instance);

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
            private readonly HashSet<Node> emittedAnchoredNodes = new HashSet<Node>(ReferenceEqualityComparer<Node>.Instance);

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
                    var path = currentPath.GetCurrentPath();
                    var tag = schema.IsTagImplicit(scalar, path, out var style) ? TagName.Empty : scalar.Tag;

                    emitter.Emit(new Events.Scalar(anchor, tag, scalar.Value, style));
                }
                return default;
            }

            public Empty Visit(Sequence sequence)
            {
                if (!TryEmitAlias(sequence, out var anchor))
                {
                    var path = currentPath.GetCurrentPath();
                    var tag = schema.IsTagImplicit(sequence, path, out var style) ? TagName.Empty : sequence.Tag;

                    var sequenceStart = new SequenceStart(anchor, tag, style);
                    emitter.Emit(sequenceStart);
                    currentPath.Push(sequenceStart);
                    foreach (var item in sequence)
                    {
                        item.Accept(this);
                    }
                    currentPath.Pop();
                    emitter.Emit(new SequenceEnd());
                }
                return default;
            }

            public Empty Visit(Mapping mapping)
            {
                if (!TryEmitAlias(mapping, out var anchor))
                {
                    var path = currentPath.GetCurrentPath();
                    var tag = schema.IsTagImplicit(mapping, path, out var style) ? TagName.Empty : mapping.Tag;

                    var mappingStart = new MappingStart(anchor, tag, style);
                    emitter.Emit(mappingStart);
                    currentPath.Push(mappingStart);
                    foreach (var pair in mapping)
                    {
                        pair.Key.Accept(this);
                        pair.Value.Accept(this);
                    }
                    currentPath.Pop();
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