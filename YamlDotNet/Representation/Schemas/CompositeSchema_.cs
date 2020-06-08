////  This file is part of YamlDotNet - A .NET library for YAML.
////  Copyright (c) Antoine Aubry and contributors

////  Permission is hereby granted, free of charge, to any person obtaining a copy of
////  this software and associated documentation files (the "Software"), to deal in
////  the Software without restriction, including without limitation the rights to
////  use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
////  of the Software, and to permit persons to whom the Software is furnished to do
////  so, subject to the following conditions:

////  The above copyright notice and this permission notice shall be included in all
////  copies or substantial portions of the Software.

////  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
////  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
////  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
////  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
////  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
////  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
////  SOFTWARE.

//using System;
//using System.Collections.Generic;
//using System.Diagnostics.CodeAnalysis;
//using YamlDotNet.Core;
//using YamlDotNet.Core.Events;

//namespace YamlDotNet.Representation.Schemas
//{
//    public sealed class CompositeSchema : ISchema
//    {
//        private readonly IEnumerable<ISchema> schemas;

//        public CompositeSchema(IEnumerable<ISchema> schemas)
//        {
//            this.schemas = schemas ?? throw new ArgumentNullException(nameof(schemas));
//        }

//        public CompositeSchema(params ISchema[] schemas) : this((IEnumerable<ISchema>)schemas) { }

//        public bool IsTagImplicit(Scalar node, IEnumerable<INodePathSegment> path, out ScalarStyle style)
//        {
//            foreach (var schema in schemas)
//            {
//                if (schema.IsTagImplicit(node, path, out style))
//                {
//                    return true;
//                }
//            }
//            style = default;
//            return false;
//        }

//        public bool IsTagImplicit(Mapping node, IEnumerable<INodePathSegment> path, out MappingStyle style)
//        {
//            foreach (var schema in schemas)
//            {
//                if (schema.IsTagImplicit(node, path, out style))
//                {
//                    return true;
//                }
//            }
//            style = default;
//            return false;
//        }

//        public bool IsTagImplicit(Sequence node, IEnumerable<INodePathSegment> path, out SequenceStyle style)
//        {
//            foreach (var schema in schemas)
//            {
//                if (schema.IsTagImplicit(node, path, out style))
//                {
//                    return true;
//                }
//            }
//            style = default;
//            return false;
//        }

//        public INodeMapper ResolveChildMapper(object? native, IEnumerable<INodePathSegment> path)
//        {
//            throw new NotImplementedException("TODO: Discontinue ?");
//        }

//        public bool ResolveMapper(TagName tag, [NotNullWhen(true)] out INodeMapper? mapper)
//        {
//            foreach (var schema in schemas)
//            {
//                if (schema.ResolveMapper(tag, out mapper))
//                {
//                    return true;
//                }
//            }
//            mapper = default;
//            return false;
//        }

//        public bool ResolveNonSpecificTag(Core.Events.Scalar node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
//        {
//            foreach (var schema in schemas)
//            {
//                if (schema.ResolveNonSpecificTag(node, path, out resolvedTag))
//                {
//                    return true;
//                }
//            }
//            resolvedTag = default;
//            return false;
//        }

//        public bool ResolveNonSpecificTag(MappingStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
//        {
//            foreach (var schema in schemas)
//            {
//                if (schema.ResolveNonSpecificTag(node, path, out resolvedTag))
//                {
//                    return true;
//                }
//            }
//            resolvedTag = default;
//            return false;
//        }

//        public bool ResolveNonSpecificTag(SequenceStart node, IEnumerable<INodePathSegment> path, [NotNullWhen(true)] out INodeMapper? resolvedTag)
//        {
//            foreach (var schema in schemas)
//            {
//                if (schema.ResolveNonSpecificTag(node, path, out resolvedTag))
//                {
//                    return true;
//                }
//            }
//            resolvedTag = default;
//            return false;
//        }
//    }
//}
