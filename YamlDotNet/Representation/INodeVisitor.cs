namespace YamlDotNet.Representation
{
    public interface INodeVisitor
    {
        void Visit(Scalar scalar);
        void Visit(Sequence sequence);
        void Visit(Mapping mapping);
    }
}