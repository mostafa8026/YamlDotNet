namespace YamlDotNet.Representation
{
    public interface INodeVisitor<T>
    {
        T Visit(Scalar scalar);
        T Visit(Sequence sequence);
        T Visit(Mapping mapping);
    }
}