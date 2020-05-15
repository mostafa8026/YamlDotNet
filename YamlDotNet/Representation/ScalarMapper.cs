using System;
using YamlDotNet.Core;

namespace YamlDotNet.Representation
{
    // TODO: Create specific implementations and discontinue this class
    public class ScalarMapper : IScalarMapper
    {
        private readonly Func<Scalar, object?> constructor;
        private readonly Func<IScalarMapper, object?, Scalar> representer;

        public TagName Tag { get; }

        public ScalarMapper(TagName name, Func<Scalar, object?> constructor, Func<IScalarMapper, object?, Scalar> representer)
        {
            Tag = name;
            this.constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
            this.representer = representer ?? throw new ArgumentNullException(nameof(representer));
        }

        public object? Construct(Scalar node) => constructor(node);
        public Scalar Represent(object? native) => representer(this, native);

        public override string ToString()
        {
            return Tag.ToString();
        }
    }
}
