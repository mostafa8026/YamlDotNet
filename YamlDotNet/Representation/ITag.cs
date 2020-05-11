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
    public class SimpleTag<TNode> : ITag<TNode> where TNode : INode
    {
        private readonly Func<TNode, object?> constructor;
        private readonly Func<ITag<TNode>, object?, TNode> representer;

        public TagName Name { get; }

        public SimpleTag(TagName name, Func<TNode, object?> constructor, Func<ITag<TNode>, object?, TNode> representer)
        {
            Name = name;
            this.constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
            this.representer = representer ?? throw new ArgumentNullException(nameof(representer));
        }

        public object? Construct(TNode node) => constructor(node);
        public TNode Represent(object? native) => representer(this, native);

        public override string ToString()
        {
            return Name.ToString();
        }
    }
}
