using System;
using YamlDotNet.Core;

namespace YamlDotNet.Representation
{
    public sealed class Scalar : Node
    {
        public IScalarMapper Mapper { get; }
        public string Value { get; }

        public Scalar(IScalarMapper mapper, string value) : base(mapper.Tag)
        {
            Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);

        public override string ToString() => $"Scalar {Tag} '{Value}'";
    }
}