//  This file is part of YamlDotNet - A .NET library for YAML.
//  Copyright (c) Antoine Aubry and contributors

//  Permission is hereby granted, free of charge, to any person obtaining a copy of
//  this software and associated documentation files (the "Software"), to deal in
//  the Software without restriction, including without limitation the rights to
//  use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
//  of the Software, and to permit persons to whom the Software is furnished to do
//  so, subject to the following conditions:

//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.

//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.

using YamlDotNet.Core;

namespace YamlDotNet.Representation.Schemas
{
    public abstract class NodeMapper<T> : INodeMapper
    {
        public TagName Tag { get; }
        public abstract NodeKind MappedNodeKind { get; }

        protected NodeMapper(TagName tag)
        {
            Tag = tag;
        }

        public abstract T Construct(Node node);
        public abstract Node Represent(T native, ISchema schema, NodePath currentPath);

        object? INodeMapper.Construct(Node node) => Construct(node);
        Node INodeMapper.Represent(object? native, ISchema schema, NodePath currentPath) => Represent((T)native!, schema, currentPath);

        public override string ToString() => Tag.ToString();
    }
}
