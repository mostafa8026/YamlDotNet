namespace YamlDotNet.Representation
{
    public interface INode
    {
        public ITag Tag { get; }
        void Accept(INodeVisitor visitor);
    }
}