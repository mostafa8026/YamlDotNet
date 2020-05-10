using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace YamlDotNet.Representation
{
    public sealed class Mapping : INode, IReadOnlyDictionary<INode, INode>
    {
        private readonly IReadOnlyDictionary<INode, INode> items;
        public ITag<Mapping> Tag { get; }

        public Mapping(ITag<Mapping> tag, IReadOnlyDictionary<INode, INode> items)
        {
            Tag = tag ?? throw new ArgumentNullException(nameof(tag));
            this.items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public INode this[string key]
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
        public INode this[INode key] => this.items[key];
        public IEnumerable<INode> Keys => this.items.Keys;
        public IEnumerable<INode> Values => this.items.Values;

        public bool ContainsKey(INode key)
        {
            return this.items.ContainsKey(key);
        }

        public bool TryGetValue(INode key, [MaybeNullWhen(false)] out INode value)
        {
            return this.items.TryGetValue(key, out value!);
        }

        public IEnumerator<KeyValuePair<INode, INode>> GetEnumerator() => this.items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        ITag INode.Tag => Tag;
        public void Accept(INodeVisitor visitor) => visitor.Visit(this);

        public override string ToString() => $"Mapping {Tag}";
    }
}