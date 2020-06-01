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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Helpers;
using YamlDotNet.Representation;
using YamlDotNet.Representation.Schemas;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;
using YamlDotNet.Serialization.NodeTypeResolvers;
using YamlDotNet.Serialization.ObjectFactories;
using YamlDotNet.Serialization.Schemas;
using YamlDotNet.Serialization.TypeInspectors;
using YamlDotNet.Serialization.TypeResolvers;

namespace YamlDotNet.Serialization
{
    /// <summary>
    /// Creates and configures instances of <see cref="Deserializer" />.
    /// This class is used to customize the behavior of <see cref="Deserializer" />. Use the relevant methods
    /// to apply customizations, then call <see cref="Build" /> to create an instance of the deserializer
    /// with the desired customizations.
    /// </summary>
    public sealed class DeserializerBuilder : BuilderSkeleton<DeserializerBuilder>
    {
        private IObjectFactory objectFactory = new DefaultObjectFactory();
        private readonly LazyComponentRegistrationList<Nothing, INodeDeserializer> nodeDeserializerFactories;
        private readonly LazyComponentRegistrationList<Nothing, INodeTypeResolver> nodeTypeResolverFactories;
        private readonly Dictionary<TagName, Type> tagMappings;
        private bool ignoreUnmatched;

        /// <summary>
        /// Initializes a new <see cref="DeserializerBuilder" /> using the default component registrations.
        /// </summary>
        public DeserializerBuilder()
            : base(new StaticTypeResolver())
        {
            tagMappings = new Dictionary<TagName, Type>
            {
                //{ YamlTagRepository.Mapping, typeof(Dictionary<object, object>) },
                //{ YamlTagRepository.String, typeof(string) },
                //{ YamlTagRepository.Boolean, typeof(bool) },
                //{ YamlTagRepository.FloatingPoint, typeof(double) },
                //{ YamlTagRepository.Integer, typeof(int) },
                //{ YamlTagRepository.Timestamp, typeof(DateTime) }
            };

            typeInspectorFactories.Add(typeof(CachedTypeInspector), inner => new CachedTypeInspector(inner));
            typeInspectorFactories.Add(typeof(NamingConventionTypeInspector), inner => namingConvention is NullNamingConvention ? inner : new NamingConventionTypeInspector(inner, namingConvention));
            typeInspectorFactories.Add(typeof(YamlAttributesTypeInspector), inner => new YamlAttributesTypeInspector(inner));
            typeInspectorFactories.Add(typeof(YamlAttributeOverridesInspector), inner => overrides != null ? new YamlAttributeOverridesInspector(inner, overrides.Clone()) : inner);
            typeInspectorFactories.Add(typeof(ReadableAndWritablePropertiesTypeInspector), inner => new ReadableAndWritablePropertiesTypeInspector(inner));

            nodeDeserializerFactories = new LazyComponentRegistrationList<Nothing, INodeDeserializer>
            {
                { typeof(YamlConvertibleNodeDeserializer), _ => new YamlConvertibleNodeDeserializer(objectFactory) },
                { typeof(YamlSerializableNodeDeserializer), _ => new YamlSerializableNodeDeserializer(objectFactory) },
                { typeof(TypeConverterNodeDeserializer), _ => new TypeConverterNodeDeserializer(BuildTypeConverters()) },
                { typeof(ScalarNodeDeserializer), _ => new ScalarNodeDeserializer() },
                { typeof(ArrayNodeDeserializer), _ => new ArrayNodeDeserializer() },
                { typeof(DictionaryNodeDeserializer), _ => new DictionaryNodeDeserializer(objectFactory) },
                { typeof(CollectionNodeDeserializer), _ => new CollectionNodeDeserializer(objectFactory) },
                { typeof(EnumerableNodeDeserializer), _ => new EnumerableNodeDeserializer() },
                { typeof(ObjectNodeDeserializer), _ => new ObjectNodeDeserializer(objectFactory, BuildTypeInspector(), ignoreUnmatched) }
            };

            nodeTypeResolverFactories = new LazyComponentRegistrationList<Nothing, INodeTypeResolver>
            {
                { typeof(YamlConvertibleTypeResolver), _ => new YamlConvertibleTypeResolver() },
                { typeof(YamlSerializableTypeResolver), _ => new YamlSerializableTypeResolver() },
                { typeof(TagNodeTypeResolver), _ => new TagNodeTypeResolver(tagMappings) },
                { typeof(PreventUnknownTagsNodeTypeResolver), _ => new PreventUnknownTagsNodeTypeResolver() },
            };
        }

        protected override DeserializerBuilder Self { get { return this; } }

        /// <summary>
        /// Sets the <see cref="IObjectFactory" /> that will be used by the deserializer.
        /// </summary>
        public DeserializerBuilder WithObjectFactory(IObjectFactory objectFactory)
        {
            this.objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
            return this;
        }

        /// <summary>
        /// Sets the <see cref="IObjectFactory" /> that will be used by the deserializer.
        /// </summary>
        public DeserializerBuilder WithObjectFactory(Func<Type, object> objectFactory)
        {
            if (objectFactory == null)
            {
                throw new ArgumentNullException(nameof(objectFactory));
            }

            return WithObjectFactory(new LambdaObjectFactory(objectFactory));
        }

        /// <summary>
        /// Registers an additional <see cref="INodeDeserializer" /> to be used by the deserializer.
        /// </summary>
        public DeserializerBuilder WithNodeDeserializer(INodeDeserializer nodeDeserializer)
        {
            return WithNodeDeserializer(nodeDeserializer, w => w.OnTop());
        }

        /// <summary>
        /// Registers an additional <see cref="INodeDeserializer" /> to be used by the deserializer.
        /// </summary>
        /// <param name="nodeDeserializer"></param>
        /// <param name="where">Configures the location where to insert the <see cref="INodeDeserializer" /></param>
        public DeserializerBuilder WithNodeDeserializer(
            INodeDeserializer nodeDeserializer,
            Action<IRegistrationLocationSelectionSyntax<INodeDeserializer>> where
        )
        {
            if (nodeDeserializer == null)
            {
                throw new ArgumentNullException(nameof(nodeDeserializer));
            }

            if (where == null)
            {
                throw new ArgumentNullException(nameof(where));
            }

            where(nodeDeserializerFactories.CreateRegistrationLocationSelector(nodeDeserializer.GetType(), _ => nodeDeserializer));
            return this;
        }

        /// <summary>
        /// Registers an additional <see cref="INodeDeserializer" /> to be used by the deserializer.
        /// </summary>
        /// <param name="nodeDeserializerFactory">A factory that creates the <see cref="INodeDeserializer" /> based on a previously registered <see cref="INodeDeserializer" />.</param>
        /// <param name="where">Configures the location where to insert the <see cref="INodeDeserializer" /></param>
        public DeserializerBuilder WithNodeDeserializer<TNodeDeserializer>(
            WrapperFactory<INodeDeserializer, TNodeDeserializer> nodeDeserializerFactory,
            Action<ITrackingRegistrationLocationSelectionSyntax<INodeDeserializer>> where
        )
            where TNodeDeserializer : INodeDeserializer
        {
            if (nodeDeserializerFactory == null)
            {
                throw new ArgumentNullException(nameof(nodeDeserializerFactory));
            }

            if (where == null)
            {
                throw new ArgumentNullException(nameof(where));
            }

            where(nodeDeserializerFactories.CreateTrackingRegistrationLocationSelector(typeof(TNodeDeserializer), (wrapped, _) => nodeDeserializerFactory(wrapped)));
            return this;
        }

        /// <summary>
        /// Unregisters an existing <see cref="INodeDeserializer" /> of type <typeparam name="TNodeDeserializer" />.
        /// </summary>
        public DeserializerBuilder WithoutNodeDeserializer<TNodeDeserializer>()
            where TNodeDeserializer : INodeDeserializer
        {
            return WithoutNodeDeserializer(typeof(TNodeDeserializer));
        }

        /// <summary>
        /// Unregisters an existing <see cref="INodeDeserializer" /> of type <param name="nodeDeserializerType" />.
        /// </summary>
        public DeserializerBuilder WithoutNodeDeserializer(Type nodeDeserializerType)
        {
            if (nodeDeserializerType == null)
            {
                throw new ArgumentNullException(nameof(nodeDeserializerType));
            }

            nodeDeserializerFactories.Remove(nodeDeserializerType);
            return this;
        }

        /// <summary>
        /// Registers an additional <see cref="INodeTypeResolver" /> to be used by the deserializer.
        /// </summary>
        public DeserializerBuilder WithNodeTypeResolver(INodeTypeResolver nodeTypeResolver)
        {
            return WithNodeTypeResolver(nodeTypeResolver, w => w.OnTop());
        }

        /// <summary>
        /// Registers an additional <see cref="INodeTypeResolver" /> to be used by the deserializer.
        /// </summary>
        /// <param name="nodeTypeResolver"></param>
        /// <param name="where">Configures the location where to insert the <see cref="INodeTypeResolver" /></param>
        public DeserializerBuilder WithNodeTypeResolver(
            INodeTypeResolver nodeTypeResolver,
            Action<IRegistrationLocationSelectionSyntax<INodeTypeResolver>> where
        )
        {
            if (nodeTypeResolver == null)
            {
                throw new ArgumentNullException(nameof(nodeTypeResolver));
            }

            if (where == null)
            {
                throw new ArgumentNullException(nameof(where));
            }

            where(nodeTypeResolverFactories.CreateRegistrationLocationSelector(nodeTypeResolver.GetType(), _ => nodeTypeResolver));
            return this;
        }

        /// <summary>
        /// Registers an additional <see cref="INodeTypeResolver" /> to be used by the deserializer.
        /// </summary>
        /// <param name="nodeTypeResolverFactory">A factory that creates the <see cref="INodeTypeResolver" /> based on a previously registered <see cref="INodeTypeResolver" />.</param>
        /// <param name="where">Configures the location where to insert the <see cref="INodeTypeResolver" /></param>
        public DeserializerBuilder WithNodeTypeResolver<TNodeTypeResolver>(
            WrapperFactory<INodeTypeResolver, TNodeTypeResolver> nodeTypeResolverFactory,
            Action<ITrackingRegistrationLocationSelectionSyntax<INodeTypeResolver>> where
        )
            where TNodeTypeResolver : INodeTypeResolver
        {
            if (nodeTypeResolverFactory == null)
            {
                throw new ArgumentNullException(nameof(nodeTypeResolverFactory));
            }

            if (where == null)
            {
                throw new ArgumentNullException(nameof(where));
            }

            where(nodeTypeResolverFactories.CreateTrackingRegistrationLocationSelector(typeof(TNodeTypeResolver), (wrapped, _) => nodeTypeResolverFactory(wrapped)));
            return this;
        }

        /// <summary>
        /// Unregisters an existing <see cref="INodeTypeResolver" /> of type <typeparam name="TNodeTypeResolver" />.
        /// </summary>
        public DeserializerBuilder WithoutNodeTypeResolver<TNodeTypeResolver>()
            where TNodeTypeResolver : INodeTypeResolver
        {
            return WithoutNodeTypeResolver(typeof(TNodeTypeResolver));
        }

        /// <summary>
        /// Unregisters an existing <see cref="INodeTypeResolver" /> of type <param name="nodeTypeResolverType" />.
        /// </summary>
        public DeserializerBuilder WithoutNodeTypeResolver(Type nodeTypeResolverType)
        {
            if (nodeTypeResolverType == null)
            {
                throw new ArgumentNullException(nameof(nodeTypeResolverType));
            }

            nodeTypeResolverFactories.Remove(nodeTypeResolverType);
            return this;
        }

        /// <summary>
        /// Registers a tag mapping.
        /// </summary>
        public override DeserializerBuilder WithTagMapping(TagName tag, Type type)
        {
            if (tag.IsEmpty)
            {
                throw new ArgumentException("Non-specific tags cannot be maped");
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (tagMappings.TryGetValue(tag, out var alreadyRegisteredType))
            {
                throw new ArgumentException($"Type already has a registered type '{alreadyRegisteredType.FullName}' for tag '{tag}'", nameof(tag));
            }

            tagMappings.Add(tag, type);
            return this;
        }

        /// <summary>
        /// Unregisters an existing tag mapping.
        /// </summary>
        public DeserializerBuilder WithoutTagMapping(TagName tag)
        {
            if (tag.IsEmpty)
            {
                throw new ArgumentException("Non-specific tags cannot be maped");
            }

            if (!tagMappings.Remove(tag))
            {
                throw new KeyNotFoundException($"Tag '{tag}' is not registered");
            }
            return this;
        }

        /// <summary>
        /// Instructs the deserializer to ignore unmatched properties instead of throwing an exception.
        /// </summary>
        public DeserializerBuilder IgnoreUnmatchedProperties()
        {
            ignoreUnmatched = true;
            return this;
        }

        /// <summary>
        /// Creates a new <see cref="Deserializer" /> according to the current configuration.
        /// </summary>
        public IDeserializer Build()
        {
            INodeMapper GetBaseMapper(TagName tag)
            {
                return schema.ResolveMapper(tag, out var mapper)
                    ? mapper
                    : throw new Exception($"Mapper for tag '{tag}' not found.");
            }

            var integerMapper = GetBaseMapper(YamlTagRepository.Integer);
            var floatingPointMapper = GetBaseMapper(YamlTagRepository.FloatingPoint);
            var stringMapper = GetBaseMapper(YamlTagRepository.String);
            var booleanMapper = GetBaseMapper(YamlTagRepository.Boolean);

            var tagNameResolver = new CompositeTagNameResolver(
                new TableTagNameResolver(tagMappings.ToDictionary(p => p.Value, p => p.Key).AsReadonlyDictionary()),
                TypeNameTagNameResolver.Instance
            );

            var typeMatchers = new TypeMatcherTable(requireThreadSafety: true) // TODO: Configure requireThreadSafety
            {
                { typeof(long), integerMapper }, // This is the 'main' one and needs to go first
                { typeof(sbyte), integerMapper },
                { typeof(byte), integerMapper },
                { typeof(short), integerMapper },
                { typeof(ushort), integerMapper },
                { typeof(int), integerMapper },
                { typeof(uint), integerMapper },
                { typeof(ulong), integerMapper },

                { typeof(double), floatingPointMapper }, // This is the 'main' one and needs to go first
                { typeof(float), floatingPointMapper },

                { typeof(string), stringMapper }, // This is the 'main' one and needs to go first
                { typeof(char), stringMapper }, // TODO: Test this

                { typeof(bool), booleanMapper },

                {
                    typeof(ICollection<>),
                    (concrete, iCollection, lookupMatcher) =>
                    {
                        var itemType = iCollection.GetGenericArguments()[0];
                        return new NodeKindMatcher<INodeMapper>(NodeKind.Sequence, SequenceMapper.Create(concrete, itemType))
                        {
                            lookupMatcher(itemType)
                        };
                    }
                },
                {
                    typeof(IDictionary<,>),
                    (concrete, iDictionary, lookupMatcher) =>
                    {
                        var types = iDictionary.GetGenericArguments();
                        var keyType = types[0];
                        var valueType = types[1];

                        var keyMapper = lookupMatcher(keyType).Value;

                        return new NodeKindMatcher<INodeMapper>(NodeKind.Mapping, MappingMapper.Create(concrete, keyType, valueType))
                        {
                            new NodeKindMatcher<INodeMapper>(keyMapper.MappedNodeKind, keyMapper)
                            {
                                lookupMatcher(valueType)
                            }
                        };
                    }
                },
                {
                    typeof(object),
                    (concrete, _, lookupMatcher) =>
                    {
                        if (!tagNameResolver.Resolve(concrete, out var tag))
                        {
                            throw new ArgumentException($"Could not resolve a tag for type '{concrete.FullName}'.");
                        }
                        var mapper = new ObjectMapper(concrete, tag);
                        return new NodeKindMatcher<INodeMapper>(mapper.MappedNodeKind, mapper);
                    }
                }
            };

            return new Deserializer(typeMatchers, schema);
        }
    }
}