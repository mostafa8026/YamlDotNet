using System;

namespace YamlDotNet.Representation
{
    public sealed class Scalar : INode
    {
        public ITag<Scalar> Tag { get; }
        public string Value { get; }

        public Scalar(ITag<Scalar> tag, string value)
        {
            Tag = tag;
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        ITag INode.Tag => Tag;
        public void Accept(INodeVisitor visitor) => visitor.Visit(this);

        public override string ToString() => $"Scalar {Tag} '{Value}'";
    }
}