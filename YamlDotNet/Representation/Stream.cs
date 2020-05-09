using System;
using System.Collections;
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

            private delegate bool ResolveNonSpecificTagDelegate<TNode>(TNode node, IEnumerable<CollectionEvent> path, [NotNullWhen(true)] out ITag? resolvedTag);

            private ITag ResolveTag<TNode>(TNode node, IEnumerable<CollectionEvent> path, ResolveNonSpecificTagDelegate<TNode> resolveNonSpecificTag)
                where TNode : NodeEvent
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
                    if (schema.ResolveSpecificTag(tag, out var resolvedTag))
                    {
                        return resolvedTag;
                    }
                }
                return new SimpleTag(tag); // TODO: Maybe use a different type of tag
            }

            public Node Visit(Events.Scalar scalar)
            {
                var path = currentPath.GetCurrentPath();
                var tag = ResolveTag(scalar, path, schema.ResolveNonSpecificTag);
                return new Scalar(tag, scalar.Value);
            }

            public Node Visit(SequenceStart sequenceStart)
            {
                var path = currentPath.GetCurrentPath();
                var tag = ResolveTag(sequenceStart, path, schema.ResolveNonSpecificTag);

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
                var path = currentPath.GetCurrentPath();
                var tag = ResolveTag(mappingStart, path, schema.ResolveNonSpecificTag);

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

        private sealed class AnchorAssigner : INodeVisitor
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

            private void VisitNode(Node node)
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
            private readonly Dictionary<Node, AnchorName> anchors;
            private readonly HashSet<Node> emittedAnchoredNodes = new HashSet<Node>(ReferenceEqualityComparer<Node>.Instance);

            public NodeDumper(IEmitter emitter, ISchema schema, Dictionary<Node, AnchorName> anchors)
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

    public interface INodeVisitor
    {
        void Visit(Scalar scalar);
        void Visit(Sequence sequence);
        void Visit(Mapping mapping);
    }

    public sealed class Document
    {
        public Node Content { get; }
        public ISchema Schema { get; }

        public Document(Node content, ISchema schema)
        {
            this.Content = content ?? throw new ArgumentNullException(nameof(content));
            this.Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }
    }

    public abstract class Node
    {
        public ITag Tag { get; }

        protected Node(ITag tag)
        {
            this.Tag = tag ?? throw new ArgumentNullException(nameof(tag));
        }

        public abstract void Accept(INodeVisitor visitor);
    }

    public sealed class Scalar : Node
    {
        public string Value { get; }

        public Scalar(ITag tag, string value)
            : base(tag)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
    }

    public sealed class Sequence : Node, IReadOnlyList<Node>
    {
        private readonly IReadOnlyList<Node> items;

        public Sequence(ITag tag, IReadOnlyList<Node> items)
            : base(tag)
        {
            this.items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public int Count => this.items.Count;
        public Node this[int index] => this.items[index];

        public IEnumerator<Node> GetEnumerator() => this.items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
    }

    public sealed class Mapping : Node, IReadOnlyDictionary<Node, Node>
    {
        private readonly IReadOnlyDictionary<Node, Node> items;

        public Mapping(ITag tag, IReadOnlyDictionary<Node, Node> items)
            : base(tag)
        {
            this.items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public Node this[string key]
        {
            get
            {
                foreach (var item in this)
                {
                    if (item.Key is Scalar scalar && scalar.Value == key)
                    {
                        return item.Value;
                    }
                }
                throw new KeyNotFoundException($"Key '{key}' not found.");
            }
        }

        public int Count => this.items.Count;
        public Node this[Node key] => this.items[key];
        public IEnumerable<Node> Keys => this.items.Keys;
        public IEnumerable<Node> Values => this.items.Values;

        public bool ContainsKey(Node key)
        {
            return this.items.ContainsKey(key);
        }

        public bool TryGetValue(Node key, [MaybeNullWhen(false)] out Node value)
        {
            return this.items.TryGetValue(key, out value!);
        }

        public IEnumerator<KeyValuePair<Node, Node>> GetEnumerator() => this.items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
    }
}