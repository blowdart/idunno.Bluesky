// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace idunno.AtProto.Json;

/// <summary>
/// Adapts polymorphic JSON contracts to tolerate unknown dollar-prefixed properties.
/// </summary>
/// <remarks>
/// <para>AT Protocol records can contain client extensions such as <c>$via</c>. System.Text.Json rejects
/// unrecognized dollar-prefixed names on polymorphic objects before they can reach extension data.
/// This factory wraps those contracts, temporarily renaming unknown metadata properties when extension
/// data is available and omitting them otherwise.</para>
/// <para>Normalization follows the selected concrete contract through nested objects and collections.
/// Discriminators, declared properties, and opaque JSON values remain unchanged, leaving type dispatch
/// and value validation to the original serializer. Deserialization callbacks restore extension names
/// on their owning objects before existing callbacks run.</para>
/// <para>The implementation uses resolver-provided contracts, including source-generated metadata,
/// rather than constructing closed generic converters at runtime. Payloads requiring no normalization
/// bypass the document and rewritten-buffer allocations.</para>
/// </remarks>
internal sealed class TolerantPolymorphicConverter(IJsonTypeInfoResolver resolver) : JsonConverterFactory
{
    private readonly JsonSerializerOptions _probeOptions = new() { TypeInfoResolver = resolver };
    private readonly ConcurrentDictionary<Type, bool> _polymorphicTypes = new();

    internal static JsonSerializerOptions FinalizeOptions(JsonSerializerOptions options)
    {
        JsonSerializerOptions result = new(options);
        Configure(result, trackChanges: false);
        result.MakeReadOnly();
        return result;
    }

    internal static void Configure(JsonSerializerOptions options, bool trackChanges = true)
    {
        List<IJsonTypeInfoResolver> initialResolvers = [];
        foreach (IJsonTypeInfoResolver chainResolver in options.TypeInfoResolverChain)
        {
            // Copy SDK resolver state instead of freezing the caller's resolver or retaining its live chain.
            IJsonTypeInfoResolver snapshot = chainResolver is TolerantTypeInfoResolver configuredResolver
                ? configuredResolver.Snapshot()
                : chainResolver;
            if (!initialResolvers.Contains(snapshot))
            {
                initialResolvers.Add(snapshot);
            }
        }

        for (int i = options.Converters.Count - 1; i >= 0; i--)
        {
            if (options.Converters[i] is TolerantPolymorphicConverter)
            {
                options.Converters.RemoveAt(i);
            }
        }

        IJsonTypeInfoResolver resolver = new TolerantTypeInfoResolver(trackChanges ? options : null, [.. initialResolvers]);
        options.Converters.Insert(0, new TolerantPolymorphicConverter(resolver));
        options.TypeInfoResolver = resolver;
    }

    public override bool CanConvert(Type typeToConvert) =>
        _polymorphicTypes.GetOrAdd(typeToConvert, type => resolver.GetTypeInfo(type, _probeOptions)?.PolymorphismOptions is not null);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        // Delegating with the caller's options would re-enter this factory. Remove all instances,
        // including those installed by chained SDK resolvers, from the underlying options.
        JsonSerializerOptions underlyingOptions = new(options);
        for (int i = underlyingOptions.Converters.Count - 1; i >= 0; i--)
        {
            if (underlyingOptions.Converters[i] is TolerantPolymorphicConverter)
            {
                underlyingOptions.Converters.RemoveAt(i);
            }
        }
        underlyingOptions.MakeReadOnly();

        JsonSerializerOptions nestedOptions = new(underlyingOptions)
        {
            TypeInfoResolver = underlyingOptions.TypeInfoResolver!.WithAddedModifier(info =>
            {
                if (info.Kind != JsonTypeInfoKind.Object)
                {
                    return;
                }

                foreach (JsonPropertyInfo property in info.Properties.Where(property => property.CustomConverter is not null))
                {
                    property.CustomConverter = new PropertyConverter(property, underlyingOptions);
                }
            })
        };
        nestedOptions.Converters.Insert(0, new CustomConverterForwarder(underlyingOptions, options));
        nestedOptions.MakeReadOnly();
        return new Converter(typeToConvert, nestedOptions);
    }

    // A property converter cannot be invoked through JsonConverter's non-generic API. A single
    // synthetic property lets the serializer invoke the original converter, including factories and
    // null handling, with SDK options, without constructing generic types through reflection.
    private sealed class PropertyConverter : JsonConverter<object>
    {
        private readonly Type _type;
        private readonly JsonTypeInfo<PropertyValue> _typeInfo;

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The property type's resolver-provided metadata is cached on read-only options before CreateJsonPropertyInfo, which uses the cached metadata instead of reflection.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The property type's resolver-provided metadata is cached on read-only options before CreateJsonPropertyInfo, avoiding its runtime generic reflection fallback.")]
        public PropertyConverter(JsonPropertyInfo property, JsonSerializerOptions underlyingOptions)
            : this(property.PropertyType, property.CustomConverter!, property.NumberHandling, underlyingOptions)
        {
        }

        public PropertyConverter(Type type, JsonConverter converter, JsonSerializerOptions underlyingOptions)
            : this(type, converter, null, underlyingOptions)
        {
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Resolver-provided metadata is cached on read-only options before CreateJsonPropertyInfo, which uses the cached metadata instead of reflection.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Resolver-provided metadata is cached on read-only options before CreateJsonPropertyInfo, avoiding its runtime generic reflection fallback.")]
        private PropertyConverter(Type type, JsonConverter converter, JsonNumberHandling? numberHandling, JsonSerializerOptions underlyingOptions)
        {
            _type = type;
            JsonSerializerOptions bridgeOptions = new(underlyingOptions);
            Configure(bridgeOptions);
            bridgeOptions.MakeReadOnly();
            // CreateJsonPropertyInfo delegates to cached typed metadata only on read-only options.
            // Prime that cache explicitly to avoid its runtime generic reflection fallback.
            _ = bridgeOptions.GetTypeInfo(_type);
            _typeInfo = JsonMetadataServices.CreateObjectInfo<PropertyValue>(bridgeOptions, new JsonObjectInfoValues<PropertyValue>
            {
                ObjectCreator = () => new PropertyValue(),
                PropertyMetadataInitializer = _ => []
            });
            JsonPropertyInfo value = _typeInfo.CreateJsonPropertyInfo(_type, "value");
            value.CustomConverter = converter;
            value.NumberHandling = numberHandling;
            value.Get = holder => ((PropertyValue)holder).Value;
            value.Set = (holder, item) => ((PropertyValue)holder).Value = item;
            value.ShouldSerialize = (_, _) => true;
            _typeInfo.Properties.Add(value);
            _typeInfo.MakeReadOnly();
        }

        public override bool HandleNull => true;

        public override bool CanConvert(Type typeToConvert) => typeToConvert == _type;

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            ArrayBufferWriter<byte> buffer = new();
            using (Utf8JsonWriter writer = new(buffer))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("value");
                document.RootElement.WriteTo(writer);
                writer.WriteEndObject();
            }

            return JsonSerializer.Deserialize(buffer.WrittenSpan, _typeInfo)!.Value;
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(new PropertyValue { Value = value }, _typeInfo);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("value").WriteTo(writer);
        }
    }

    private sealed class PropertyValue
    {
        public object? Value { get; set; }
    }

    // Opaque converters have no property contracts to traverse. Forward them to the SDK options so
    // any nested records they deserialize themselves still receive tolerant handling.
    private sealed class CustomConverterForwarder(JsonSerializerOptions probeOptions, JsonSerializerOptions sdkOptions) : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            Type valueType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
            return !valueType.IsEnum &&
                GetConverter(typeToConvert) is JsonConverter converter &&
                converter.GetType().Assembly != typeof(JsonSerializer).Assembly;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            new ForwardingConverter(typeToConvert, GetConverter(typeToConvert)!, sdkOptions);

        private JsonConverter? GetConverter(Type typeToConvert)
        {
            foreach (JsonConverter registeredConverter in probeOptions.Converters)
            {
                if (registeredConverter is JsonConverterFactory factory)
                {
                    if (factory.CanConvert(typeToConvert))
                    {
                        return factory.CreateConverter(typeToConvert, probeOptions);
                    }
                }
                else if (registeredConverter.CanConvert(typeToConvert))
                {
                    return registeredConverter;
                }
            }

            return probeOptions.TypeInfoResolver!.GetTypeInfo(typeToConvert, probeOptions)?.Converter;
        }
    }

    private sealed class ForwardingConverter(Type type, JsonConverter converter, JsonSerializerOptions sdkOptions) : JsonConverter<object>
    {
        private readonly PropertyConverter _converter = new(type, converter, sdkOptions);

        public override bool HandleNull => true;

        public override bool CanConvert(Type typeToConvert) => typeToConvert == type;

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            _converter.Read(ref reader, typeToConvert, options);

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
            _converter.Write(writer, value, options);
    }

    /// <summary>
    /// Resolves from the original and subsequently registered resolvers without referring back to itself.
    /// </summary>
    private sealed class TolerantTypeInfoResolver(JsonSerializerOptions? sdkOptions, IJsonTypeInfoResolver[] initialResolvers) : IJsonTypeInfoResolver
    {
        private readonly object _resolverLock = new();
        private IJsonTypeInfoResolver? _combinedResolver;
        private IJsonTypeInfoResolver[]? _chainSnapshot;

        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo? info = GetCombinedResolver().GetTypeInfo(type, options);
            return info is null ? null : ApplyCustomConverterPolymorphismWorkaround(info);
        }

        public TolerantTypeInfoResolver Snapshot() => new(null, [GetCombinedResolver()]);

        private IJsonTypeInfoResolver GetCombinedResolver()
        {
            lock (_resolverLock)
            {
                if (sdkOptions is null)
                {
                    return _combinedResolver ??= JsonTypeInfoResolver.Combine(initialResolvers);
                }

                IList<IJsonTypeInfoResolver> chain = sdkOptions.TypeInfoResolverChain;
                if (_chainSnapshot is not null && _chainSnapshot.Length == chain.Count)
                {
                    int index = 0;
                    while (index < chain.Count && ReferenceEquals(_chainSnapshot[index], chain[index]))
                    {
                        index++;
                    }

                    if (index == chain.Count)
                    {
                        return _combinedResolver!;
                    }
                }

                IJsonTypeInfoResolver[] currentChain = chain.ToArray();
                List<IJsonTypeInfoResolver> resolvers = [];
                resolvers.AddRange(currentChain.Where(resolver => !ReferenceEquals(resolver, this)).Distinct());
                resolvers.AddRange(initialResolvers.Where(resolver => !resolvers.Contains(resolver)));

                _chainSnapshot = currentChain;
                _combinedResolver = JsonTypeInfoResolver.Combine(resolvers.ToArray());
                return _combinedResolver;
            }
        }

        private static JsonTypeInfo ApplyCustomConverterPolymorphismWorkaround(JsonTypeInfo info)
        {
            // A custom converter cannot also expose built-in polymorphism metadata. Keep it on the
            // underlying contract; the tolerant wrapper performs dispatch using that original contract.
            if (info.Kind == JsonTypeInfoKind.None &&
                info.PolymorphismOptions is not null &&
                info.Converter.GetType().Assembly != typeof(JsonSerializer).Assembly)
            {
                info.PolymorphismOptions = null;
            }

            return info;
        }
    }

    private sealed class Converter(Type type, JsonSerializerOptions underlyingOptions) : JsonConverter<object>
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert == type;

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            JsonTypeInfo typeInfo = underlyingOptions.GetTypeInfo(type);
            // Probe a reader copy so the original stays positioned for normal deserialization.
            Utf8JsonReader probe = reader;
            if (!NeedsNormalization(ref probe, typeInfo))
            {
                return JsonSerializer.Deserialize(ref reader, typeInfo);
            }

            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement element = document.RootElement;
            NormalizationState state = new(element);

            ArrayBufferWriter<byte> buffer = new();
            bool normalized;
            using (Utf8JsonWriter writer = new(buffer))
            {
                normalized = WriteNormalized(writer, element, underlyingOptions.GetTypeInfo(type), state);
            }
            if (!normalized)
            {
                return JsonSerializer.Deserialize(element, underlyingOptions.GetTypeInfo(type));
            }

            // Renamed properties now look like ordinary unknown fields to System.Text.Json.
            // Restore their original names per object, preserving the existing callback afterward.
            JsonSerializerOptions readOptions = new(underlyingOptions)
            {
                TypeInfoResolver = underlyingOptions.TypeInfoResolver!.WithAddedModifier(info =>
                {
                    if (info.Kind != JsonTypeInfoKind.Object)
                    {
                        return;
                    }

                    JsonPropertyInfo? extension = info.Properties.FirstOrDefault(property => property.IsExtensionData);
                    if (extension is null)
                    {
                        return;
                    }

                    Action<object>? callback = info.OnDeserialized;
                    info.OnDeserialized = value =>
                    {
                        object? data = extension.Get?.Invoke(value);
                        if (data is IDictionary<string, JsonElement> elements)
                        {
                            RestoreNames(elements, state.RenamedNames);
                        }
                        else if (data is IDictionary<string, object> objects)
                        {
                            RestoreNames(objects, state.RenamedNames);
                        }
                        else if (data is JsonObject jsonObject)
                        {
                            RestoreNames<JsonNode?>(jsonObject, state.RenamedNames);
                        }
                        callback?.Invoke(value);
                    };
                })
            };
            readOptions.MakeReadOnly();

            return JsonSerializer.Deserialize(buffer.WrittenSpan, readOptions.GetTypeInfo(type));
        }

        private sealed class NormalizationState
        {
            private readonly HashSet<string> _propertyNames = new(StringComparer.Ordinal);
            private long _nextName;

            internal NormalizationState(JsonElement element)
            {
                CollectPropertyNames(element);
            }

            internal Dictionary<string, string> RenamedNames { get; } = new(StringComparer.Ordinal);

            internal string Rename(string originalName)
            {
                string temporaryName;
                do
                {
                    temporaryName = "\0" + _nextName++.ToString(CultureInfo.InvariantCulture);
                }
                while (!_propertyNames.Add(temporaryName));

                RenamedNames.Add(temporaryName, originalName);
                return temporaryName;
            }

            private void CollectPropertyNames(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        _propertyNames.Add(property.Name);
                        CollectPropertyNames(property.Value);
                    }
                }
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        CollectPropertyNames(item);
                    }
                }
            }
        }

        private static void RestoreNames<T>(IDictionary<string, T> data, Dictionary<string, string> renamedNames)
        {
            foreach (string name in data.Keys.Where(renamedNames.ContainsKey).ToArray())
            {
                T item = data[name];
                data.Remove(name);
                data[renamedNames[name]] = item;
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, underlyingOptions.GetTypeInfo(type));

        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Collection serialization metadata preserves the implemented generic collection interface and its element type. Only interface identities and generic arguments are inspected, not interface members.")]
        private static Type? GetElementType(JsonTypeInfo info)
        {
#if NET10_0_OR_GREATER
            return info.ElementType;
#else
            if (info.Type.IsArray)
            {
                return info.Type.GetElementType();
            }

            bool isDictionary = info.Kind == JsonTypeInfoKind.Dictionary;
            Type? elementType = GetCollectionElementType(info.Type, isDictionary);
            if (elementType is not null)
            {
                return elementType;
            }

            foreach (Type implementedInterface in info.Type.GetInterfaces())
            {
                elementType = GetCollectionElementType(implementedInterface, isDictionary);
                if (elementType is not null)
                {
                    return elementType;
                }
            }

            return null;
#endif
        }

#if !NET10_0_OR_GREATER
        private static Type? GetCollectionElementType(Type type, bool isDictionary)
        {
            if (!type.IsGenericType)
            {
                return null;
            }

            Type definition = type.GetGenericTypeDefinition();
            if (isDictionary &&
                (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>)))
            {
                return type.GenericTypeArguments[1];
            }
            if (!isDictionary && definition == typeof(IEnumerable<>))
            {
                return type.GenericTypeArguments[0];
            }

            return null;
        }
#endif

        // Most payloads need no rewriting. Scan a reader copy without allocating property names or a DOM.
        private bool NeedsNormalization(ref Utf8JsonReader reader, JsonTypeInfo info)
        {
            if (reader.TokenType == JsonTokenType.StartArray && info.Kind == JsonTypeInfoKind.Enumerable &&
                GetElementType(info) is Type elementType)
            {
                JsonTypeInfo elementInfo = underlyingOptions.GetTypeInfo(elementType);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (NeedsNormalization(ref reader, elementInfo))
                    {
                        return true;
                    }
                }
                return false;
            }

            if (reader.TokenType == JsonTokenType.StartObject && info.Kind == JsonTypeInfoKind.Dictionary &&
                GetElementType(info) is Type valueType)
            {
                JsonTypeInfo valueInfo = underlyingOptions.GetTypeInfo(valueType);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    reader.Read();
                    if (NeedsNormalization(ref reader, valueInfo))
                    {
                        return true;
                    }
                }
                return false;
            }

            if (reader.TokenType != JsonTokenType.StartObject || info.Kind != JsonTypeInfoKind.Object)
            {
                SkipValue(ref reader);
                return false;
            }

            JsonPolymorphismOptions? polymorphism = info.PolymorphismOptions;
            if (polymorphism is not null)
            {
                Utf8JsonReader discriminatorReader = reader;
                while (discriminatorReader.Read() && discriminatorReader.TokenType != JsonTokenType.EndObject)
                {
                    bool isDiscriminator = discriminatorReader.ValueTextEquals(polymorphism.TypeDiscriminatorPropertyName);
                    discriminatorReader.Read();
                    if (isDiscriminator)
                    {
                        foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
                        {
                            if ((derived.TypeDiscriminator is string text && discriminatorReader.TokenType == JsonTokenType.String && discriminatorReader.ValueTextEquals(text)) ||
                                (derived.TypeDiscriminator is int number && discriminatorReader.TokenType == JsonTokenType.Number && discriminatorReader.TryGetInt32(out int value) && value == number))
                            {
                                info = underlyingOptions.GetTypeInfo(derived.DerivedType);
                                break;
                            }
                        }
                        break;
                    }
                    SkipValue(ref discriminatorReader);
                }
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                JsonPropertyInfo? contract = null;
                foreach (JsonPropertyInfo candidate in info.Properties)
                {
                    if (!candidate.IsExtensionData && (candidate.Get is not null || candidate.Set is not null) &&
                        reader.ValueTextEquals(candidate.Name))
                    {
                        contract = candidate;
                        break;
                    }
                }
                if (contract is null && underlyingOptions.PropertyNameCaseInsensitive)
                {
                    string name = reader.GetString()!;
                    foreach (JsonPropertyInfo candidate in info.Properties)
                    {
                        if (!candidate.IsExtensionData && (candidate.Get is not null || candidate.Set is not null) &&
                            string.Equals(name, candidate.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            contract = candidate;
                            break;
                        }
                    }
                }

                bool dollarName = reader.HasValueSequence || reader.ValueIsEscaped
                    ? reader.GetString()!.StartsWith('$')
                    : reader.ValueSpan.StartsWith("$"u8);
                if (polymorphism is not null && contract is null && dollarName &&
                    !reader.ValueTextEquals(polymorphism.TypeDiscriminatorPropertyName) &&
                    !(underlyingOptions.ReferenceHandler is not null &&
                        (reader.ValueTextEquals("$id") || reader.ValueTextEquals("$ref") || reader.ValueTextEquals("$values"))))
                {
                    return true;
                }

                reader.Read();
                if (contract is not null && contract.CustomConverter is null)
                {
                    if (NeedsNormalization(ref reader, underlyingOptions.GetTypeInfo(contract.PropertyType)))
                    {
                        return true;
                    }
                }
                else
                {
                    SkipValue(ref reader);
                }
            }
            return false;
        }

        private static void SkipValue(ref Utf8JsonReader reader)
        {
            if (!reader.TrySkip())
            {
                throw new JsonException("Incomplete JSON value.");
            }
        }

        // Walk declared contracts, not arbitrary JSON: extension values and custom-converted values
        // are opaque. Renaming only the owning object's unknown metadata preserves their contents.
        private bool WriteNormalized(Utf8JsonWriter writer, JsonElement element, JsonTypeInfo info, NormalizationState state)
        {
            bool normalized = false;
            if (element.ValueKind == JsonValueKind.Object && info.Kind == JsonTypeInfoKind.Dictionary && GetElementType(info) is Type valueType)
            {
                JsonTypeInfo valueInfo = underlyingOptions.GetTypeInfo(valueType);
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    normalized |= WriteNormalized(writer, property.Value, valueInfo, state);
                }
                writer.WriteEndObject();
                return normalized;
            }

            if (element.ValueKind == JsonValueKind.Array && info.Kind == JsonTypeInfoKind.Enumerable)
            {
                Type? elementType = GetElementType(info);
                if (elementType is not null)
                {
                    writer.WriteStartArray();
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        normalized |= WriteNormalized(writer, item, underlyingOptions.GetTypeInfo(elementType), state);
                    }
                    writer.WriteEndArray();
                    return normalized;
                }
            }

            if (element.ValueKind != JsonValueKind.Object || info.Kind != JsonTypeInfoKind.Object)
            {
                element.WriteTo(writer);
                return false;
            }

            JsonPolymorphismOptions? polymorphism = info.PolymorphismOptions;
            if (polymorphism is not null && element.TryGetProperty(polymorphism.TypeDiscriminatorPropertyName, out JsonElement discriminator))
            {
                foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
                {
                    if ((derived.TypeDiscriminator is string text && discriminator.ValueKind == JsonValueKind.String && discriminator.GetString() == text) ||
                        (derived.TypeDiscriminator is int number && discriminator.ValueKind == JsonValueKind.Number && discriminator.TryGetInt32(out int value) && value == number))
                    {
                        info = underlyingOptions.GetTypeInfo(derived.DerivedType);
                        break;
                    }
                }
            }

            StringComparison comparison = underlyingOptions.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            bool hasExtensionData = info.Properties.Any(property => property.IsExtensionData);
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject())
            {
                JsonPropertyInfo? contract = info.Properties.FirstOrDefault(candidate =>
                    !candidate.IsExtensionData &&
                    (candidate.Get is not null || candidate.Set is not null) &&
                    string.Equals(candidate.Name, property.Name, comparison));

                bool unknownMetadata = polymorphism is not null &&
                    property.Name.StartsWith('$') &&
                    property.Name != polymorphism.TypeDiscriminatorPropertyName &&
                    contract is null &&
                    !(underlyingOptions.ReferenceHandler is not null && property.Name is "$id" or "$ref" or "$values");

                normalized |= unknownMetadata;
                if (unknownMetadata && !hasExtensionData)
                {
                    continue;
                }

                writer.WritePropertyName(unknownMetadata && hasExtensionData ? state.Rename(property.Name) : property.Name);
                if (contract is not null && contract.CustomConverter is null)
                {
                    normalized |= WriteNormalized(writer, property.Value, underlyingOptions.GetTypeInfo(contract.PropertyType), state);
                }
                else
                {
                    property.Value.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
            return normalized;
        }
    }
}
