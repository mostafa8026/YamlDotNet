using System;
using System.Collections;
using System.Collections.Generic;

namespace YamlDotNet.Representation
{
    public sealed class Sequence : INode, IReadOnlyList<INode>
    {
        private readonly IReadOnlyList<INode> items;
        public ITag<Sequence> Tag { get; }

        public Sequence(ITag<Sequence> tag, IReadOnlyList<INode> items)
        {
            Tag = tag;
            this.items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public int Count => this.items.Count;
        public INode this[int index] => this.items[index];

        public IEnumerator<INode> GetEnumerator() => this.items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        ITag INode.Tag => Tag;
        public void Accept(INodeVisitor visitor) => visitor.Visit(this);

        public override string ToString() => $"Sequence {Tag}";
    }
}