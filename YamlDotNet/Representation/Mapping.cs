using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Core;

namespace YamlDotNet.Representation
{
    public sealed class Mapping : Node, IReadOnlyDictionary<Node, Node>
    {
        private readonly IReadOnlyDictionary<Node, Node> items;

        public Mapping(TagName tag, IReadOnlyDictionary<Node, Node> items) : base(tag)
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

        public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);

        public override string ToString() => $"Mapping {Tag}";
    }
}