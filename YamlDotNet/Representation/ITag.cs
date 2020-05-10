using System;
using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Core;

namespace YamlDotNet.Representation
{
    public interface ITag
    {
        TagName Name { get; }
    }

    public interface ITag<TNode> : ITag where TNode : INode
    {
        object? Construct(TNode node);
        TNode Represent(object? native);
    }

    // TODO: Create specific implementations and discontinue this class
    public sealed class SimpleTag<TNode> : ITag<TNode> where TNode : INode
    {
        private readonly Func<TNode, object?> constructor;

        public TagName Name { get; }

        public SimpleTag(TagName name, Func<TNode, object?> constructor)
        {
            Name = name;
            this.constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
        }

        public object? Construct(TNode node) => constructor(node);

        public TNode Represent(object? native)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return Name.ToString();
        }
    }
}
