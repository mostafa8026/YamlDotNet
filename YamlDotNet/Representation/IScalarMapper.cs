using YamlDotNet.Core;

namespace YamlDotNet.Representation
{
    public interface IScalarMapper
    {
        /// <summary>
        /// The tag to which this mapper applies.
        /// </summary>
        TagName Tag { get; }

        /// <summary>
        /// Constructs a native representation of the value contained by the specified <see cref="Scalar" />.
        /// </summary>
        /// <param name="node">The <see cref="Scalar" /> to be converted. It's tag must be equal to <see cref="Tag" />.</param>
        /// <returns>
        /// Returns a native representation of the scalar. The ative representation could be, for example,
        /// a <see cref="System.Int32" />, if the scalar represents a number.
        /// </returns>
        object? Construct(Scalar node);

        /// <summary>
        /// Creates a <see cref="Scalar"/> that represents the specified native value.
        /// </summary>
        /// <param name="native">The value to be converted. It must be compatible with this mapper's <see cref="Tag"/>.</param>
        /// <returns>
        /// Returns a <see cref="Scalar"/> that represents the <paramref name="native"/> in YAML,
        /// according to this <see cref="Tag"/>'s rules.
        /// </returns>
        Scalar Represent(object? native);
    }
}
