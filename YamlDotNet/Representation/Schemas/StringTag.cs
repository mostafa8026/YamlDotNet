using YamlDotNet.Core;

namespace YamlDotNet.Representation.Schemas
{
    /// <summary>
    /// The tag:yaml.org,2002:str tag, as specified in the Failsafe schema.
    /// </summary>
    /// <remarks>
    /// Use <see cref="System.String" /> as native representation.
    /// </remarks>
    public sealed class StringTag : IScalarMapper
    {
        public static readonly StringTag Instance = new StringTag();

        private StringTag() { }

        public TagName Tag => YamlTagRepository.String;

        public object? Construct(Scalar node)
        {
            return node.Value;
        }

        public Scalar Represent(object? native)
        {
            return new Scalar(this, (string)native!);
        }
    }
}
