using System;
using System.Collections;
using System.Collections.Generic;
using YamlDotNet.Core;

namespace YamlDotNet.Representation
{
    public sealed class Sequence : Node, IReadOnlyList<Node>
    {
        private readonly IReadOnlyList<Node> items;

        public Sequence(TagName tag, IReadOnlyList<Node> items) : base(tag)
        {
            this.items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public int Count => this.items.Count;
        public Node this[int index] => this.items[index];

        public IEnumerator<Node> GetEnumerator() => this.items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);

        public override string ToString() => $"Sequence {Tag}";
    }
}