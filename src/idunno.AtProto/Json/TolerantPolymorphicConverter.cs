// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Collections.Concurrent;
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

    internal static void Configure(JsonSerializerOptions options)
    {
        // .NET 9 exposes a live, options-bound resolver chain. Snapshot it before assigning a
        // wrapper back to the options, or that assignment makes the chain contain its own wrapper.
        IJsonTypeInfoResolver resolver = JsonTypeInfoResolver.Combine(options.TypeInfoResolverChain.ToArray());
        options.Converters.Insert(0, new TolerantPolymorphicConverter(resolver));
        options.TypeInfoResolver = resolver.WithAddedModifier(info =>
        {
            // A custom converter cannot also expose built-in polymorphism metadata. Remove it only from
            // our wrapper contract; the underlying contract retains it for dispatch and validation.
            if (info.Kind == JsonTypeInfoKind.None &&
                info.Options.Converters.OfType<TolerantPolymorphicConverter>().Any(converter => converter.CanConvert(info.Type)))
            {
                info.PolymorphismOptions = null;
            }
        });
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

        JsonSerializerOptions nestedOptions = new(underlyingOptions);
        nestedOptions.Converters.Insert(0, new CustomConverterForwarder(underlyingOptions, options));
        nestedOptions.MakeReadOnly();
        return new Converter(typeToConvert, nestedOptions);
    }

    // Opaque converters have no property contracts to traverse. Forward them to the SDK options so
    // any nested records they deserialize themselves still receive tolerant handling.
    private sealed class CustomConverterForwarder(JsonSerializerOptions probeOptions, JsonSerializerOptions sdkOptions) : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (typeToConvert.IsValueType)
            {
                return false;
            }

            JsonTypeInfo? info = probeOptions.TypeInfoResolver!.GetTypeInfo(typeToConvert, probeOptions);
            return info?.Kind == JsonTypeInfoKind.None && info.Converter.GetType().Assembly != typeof(JsonSerializer).Assembly;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            new ForwardingConverter(typeToConvert, sdkOptions);
    }

    private sealed class ForwardingConverter(Type type, JsonSerializerOptions sdkOptions) : JsonConverter<object>
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert == type;

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            JsonSerializer.Deserialize(ref reader, sdkOptions.GetTypeInfo(type));

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, sdkOptions.GetTypeInfo(type));
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
            int prefixLength = 1;
            string prefix = "\0";
            // A prefix absent from every input property makes renaming reversible even when the
            // payload already contains null-prefixed names. No user-visible names are reserved.
            while (ContainsPrefix(element, prefix))
            {
                prefix = new string('\0', ++prefixLength);
            }

            ArrayBufferWriter<byte> buffer = new();
            bool normalized;
            using (Utf8JsonWriter writer = new(buffer))
            {
                normalized = WriteNormalized(writer, element, underlyingOptions.GetTypeInfo(type), prefix);
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
                            RestoreNames(elements, prefix);
                        }
                        else if (data is IDictionary<string, object> objects)
                        {
                            RestoreNames(objects, prefix);
                        }
                        else if (data is JsonObject jsonObject)
                        {
                            RestoreNames<JsonNode?>(jsonObject, prefix);
                        }
                        callback?.Invoke(value);
                    };
                })
            };
            readOptions.MakeReadOnly();

            return JsonSerializer.Deserialize(buffer.WrittenSpan, readOptions.GetTypeInfo(type));
        }

        private static void RestoreNames<T>(IDictionary<string, T> data, string prefix)
        {
            foreach (string name in data.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            {
                T item = data[name];
                data.Remove(name);
                data.Add(name[prefix.Length..], item);
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, underlyingOptions.GetTypeInfo(type));

        private static Type? GetElementType(JsonTypeInfo info)
        {
#if NET10_0_OR_GREATER
            return info.ElementType;
#else
            return info.Type.IsArray
                ? info.Type.GetElementType()
                : info.Type.GenericTypeArguments.LastOrDefault();
#endif
        }

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

        private static bool ContainsPrefix(JsonElement element, string prefix) => element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().Any(property =>
                property.Name.StartsWith(prefix, StringComparison.Ordinal) || ContainsPrefix(property.Value, prefix)),
            JsonValueKind.Array => element.EnumerateArray().Any(item => ContainsPrefix(item, prefix)),
            _ => false
        };

        // Walk declared contracts, not arbitrary JSON: extension values and custom-converted values
        // are opaque. Renaming only the owning object's unknown metadata preserves their contents.
        private bool WriteNormalized(Utf8JsonWriter writer, JsonElement element, JsonTypeInfo info, string prefix)
        {
            bool normalized = false;
            if (element.ValueKind == JsonValueKind.Object && info.Kind == JsonTypeInfoKind.Dictionary && GetElementType(info) is Type valueType)
            {
                JsonTypeInfo valueInfo = underlyingOptions.GetTypeInfo(valueType);
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    normalized |= WriteNormalized(writer, property.Value, valueInfo, prefix);
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
                        normalized |= WriteNormalized(writer, item, underlyingOptions.GetTypeInfo(elementType), prefix);
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

                writer.WritePropertyName(unknownMetadata ? prefix + property.Name : property.Name);
                if (contract is not null && contract.CustomConverter is null)
                {
                    normalized |= WriteNormalized(writer, property.Value, underlyingOptions.GetTypeInfo(contract.PropertyType), prefix);
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
