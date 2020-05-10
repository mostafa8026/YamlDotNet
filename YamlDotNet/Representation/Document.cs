using System;
using YamlDotNet.Representation.Schemas;

namespace YamlDotNet.Representation
{
    public sealed class Document
    {
        public INode Content { get; }
        public ISchema Schema { get; }

        public Document(INode content, ISchema schema)
        {
            this.Content = content ?? throw new ArgumentNullException(nameof(content));
            this.Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }
    }
}