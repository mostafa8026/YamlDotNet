using YamlDotNet.Core;

namespace YamlDotNet.Representation
{
    public abstract class Node
    {
        // Prevent extending this class from outside
        internal Node(TagName tag)
        {
            Tag = tag;
        }

        public TagName Tag { get; }
        public abstract T Accept<T>(INodeVisitor<T> visitor);
    }
}