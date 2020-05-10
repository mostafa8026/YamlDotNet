using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        public static void Dump(IEmitter emitter, IEnumerable<Document> stream)
        {
            emitter.Emit(new StreamStart());
            foreach (var document in stream)
            {
                emitter.Emit(new DocumentStart());

                var count = 0;
                var anchorAssigner = new AnchorAssigner(_ => $"n{count++}");
                document.Content.Accept(anchorAssigner);

                var dumper = new NodeDumper(emitter, document.Schema, anchorAssigner.GetAssignedAnchors());
                document.Content.Accept(dumper);

                emitter.Emit(new DocumentEnd(true));
            }
            emitter.Emit(new StreamEnd());
        }

        private sealed class NodeLoader : IParsingEventVisitor<INode>
        {
            private readonly Dictionary<AnchorName, INode> anchoredNodes = new Dictionary<AnchorName, INode>();
            private readonly NodePath currentPath = new NodePath();
            private readonly IParser parser;
            private readonly ISchema schema;

            public NodeLoader(IParser parser, ISchema schema)
            {
                this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
                this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            }

            public INode Visit(AnchorAlias anchorAlias)
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

            private delegate bool ResolveNonSpecificTagDelegate<TEvent, TNode>(TEvent @event, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag<TNode>? resolvedTag)
                where TEvent : NodeEvent
                where TNode : INode;

            private delegate bool ResolveSpecificTagDelegate<TNode>(TagName tag, [NotNullWhen(true)] out ITag<TNode>? resolvedTag)
                where TNode : INode;

            private ITag<TNode> ResolveTag<TEvent, TNode>(TEvent node, IEnumerable<CollectionEvent> path, ResolveNonSpecificTagDelegate<TEvent, TNode> resolveNonSpecificTag, ResolveSpecificTagDelegate<TNode> resolveSpecificTag)
                where TEvent : NodeEvent
                where TNode : INode
            {
                var tag = node.Tag;
                if (tag.IsNonSpecific)
                {
                    if (resolveNonSpecificTag(node, path, out var resolvedTag))
                    {
                        return resolvedTag;
                    }
                }
                else
                {
                    if (resolveSpecificTag(tag, out var resolvedTag))
                    {
                        return resolvedTag;
                    }
                }
                return new SimpleTag<TNode>(tag, _ => throw new NotImplementedException()); // TODO: Maybe use a different type of tag
            }

            public INode Visit(Events.Scalar scalar)
            {
                var path = currentPath.GetCurrentPath();
                var tag = ResolveTag<Events.Scalar, Scalar>(scalar, path, schema.ResolveNonSpecificTag, schema.ResolveSpecificTag);
                return new Scalar(tag, scalar.Value);
            }

            public INode Visit(SequenceStart sequenceStart)
            {
                var path = currentPath.GetCurrentPath();
                var tag = ResolveTag<SequenceStart, Sequence>(sequenceStart, path, schema.ResolveNonSpecificTag, schema.ResolveSpecificTag);

                currentPath.Push(sequenceStart);

                var items = new List<INode>();
                while (parser.TryConsume<NodeEvent>(out var nodeEvent))
                {
                    var child = nodeEvent.Accept(this);
                    items.Add(child);
                }
                parser.Consume<SequenceEnd>();

                currentPath.Pop();

                return new Sequence(tag, items.AsReadonly());
            }

            public INode Visit(MappingStart mappingStart)
            {
                var path = currentPath.GetCurrentPath();
                var tag = ResolveTag<MappingStart, Mapping>(mappingStart, path, schema.ResolveNonSpecificTag, schema.ResolveSpecificTag);

                currentPath.Push(mappingStart);

                var items = new Dictionary<INode, INode>();
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

            public INode Visit(Comment comment) => throw UnexpectedEvent(comment);
            public INode Visit(SequenceEnd sequenceEnd) => throw UnexpectedEvent(sequenceEnd);
            public INode Visit(MappingEnd mappingEnd) => throw UnexpectedEvent(mappingEnd);
            public INode Visit(DocumentStart documentStart) => throw UnexpectedEvent(documentStart);
            public INode Visit(DocumentEnd documentEnd) => throw UnexpectedEvent(documentEnd);
            public INode Visit(StreamStart streamStart) => throw UnexpectedEvent(streamStart);
            public INode Visit(StreamEnd streamEnd) => throw UnexpectedEvent(streamEnd);
        }

        private sealed class AnchorAssigner : INodeVisitor
        {
            private readonly Func<INode, AnchorName> assignAnchor;
            private readonly HashSet<INode> encounteredNodes = new HashSet<INode>(ReferenceEqualityComparer<INode>.Instance);
            private readonly Dictionary<INode, AnchorName> assignedAnchors = new Dictionary<INode, AnchorName>(ReferenceEqualityComparer<INode>.Instance);

            public AnchorAssigner(Func<INode, AnchorName> assignAnchor)
            {
                this.assignAnchor = assignAnchor ?? throw new ArgumentNullException(nameof(assignAnchor));
            }

            public Dictionary<INode, AnchorName> GetAssignedAnchors()
            {
                return assignedAnchors;
            }

            private void VisitNode(INode node)
            {
                if (!encounteredNodes.Add(node))
                {
                    assignedAnchors.Add(node, assignAnchor(node));
                }
            }

            void INodeVisitor.Visit(Scalar scalar) => VisitNode(scalar);
            void INodeVisitor.Visit(Sequence sequence) => VisitNode(sequence);
            void INodeVisitor.Visit(Mapping mapping) => VisitNode(mapping);
        }

        private sealed class NodeDumper : INodeVisitor
        {
            private readonly IEmitter emitter;
            private readonly ISchema schema;
            private readonly Dictionary<INode, AnchorName> anchors;
            private readonly HashSet<INode> emittedAnchoredNodes = new HashSet<INode>(ReferenceEqualityComparer<INode>.Instance);

            public NodeDumper(IEmitter emitter, ISchema schema, Dictionary<INode, AnchorName> anchors)
            {
                this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
                this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
                this.anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
            }

            public void Visit(Scalar scalar)
            {
                if (!TryEmitAlias(scalar, out var anchor))
                {
                    // TODO: Style ?
                    emitter.Emit(new Events.Scalar(anchor, scalar.Tag.Name, scalar.Value));
                }
            }

            public void Visit(Sequence sequence)
            {
                if (!TryEmitAlias(sequence, out var anchor))
                {
                    // TODO: Style ?
                    emitter.Emit(new SequenceStart(anchor, sequence.Tag.Name, SequenceStyle.Any));
                    foreach (var item in sequence)
                    {
                        item.Accept(this);
                    }
                    emitter.Emit(new SequenceEnd());
                }
            }

            public void Visit(Mapping mapping)
            {
                if (!TryEmitAlias(mapping, out var anchor))
                {
                    // TODO: Style ?
                    emitter.Emit(new MappingStart(anchor, mapping.Tag.Name, MappingStyle.Any));
                    foreach (var pair in mapping)
                    {
                        pair.Key.Accept(this);
                        pair.Value.Accept(this);
                    }
                    emitter.Emit(new MappingEnd());
                }
            }

            private bool TryEmitAlias(INode node, out AnchorName anchor)
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