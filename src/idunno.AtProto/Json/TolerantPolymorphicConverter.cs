// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
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
/// This factory wraps object and collection roots, temporarily renaming unknown metadata properties on
/// polymorphic contracts when extension data is available and omitting them otherwise.</para>
/// <para>Normalization follows the selected concrete contract through nested objects and collections.
/// Discriminators, declared properties, and opaque JSON values remain unchanged, leaving type dispatch
/// and value validation to the original serializer. Deserialization callbacks restore extension names
/// on their owning objects before existing callbacks run.</para>
/// <para>Reference-preserving options normalize the complete root before delegating to one built-in
/// serializer operation, retaining shared references across nested objects and collections.</para>
/// <para>The implementation uses resolver-provided contracts, including source-generated metadata,
/// rather than constructing closed generic converters at runtime. Payloads requiring no normalization
/// bypass the document and rewritten-buffer allocations.</para>
/// </remarks>
internal sealed class TolerantPolymorphicConverter(IJsonTypeInfoResolver resolver, JsonSerializerOptions configuredOptions) : JsonConverterFactory
{
    private readonly Lazy<JsonSerializerOptions> _probeOptions = new(() => CreateProbeOptions(resolver, configuredOptions));
    private readonly ConcurrentDictionary<(Type Type, bool PreserveReferences), bool> _supportedTypes = new();

    private static JsonSerializerOptions CreateProbeOptions(IJsonTypeInfoResolver resolver, JsonSerializerOptions configuredOptions)
    {
        JsonSerializerOptions probeOptions = new(configuredOptions) { TypeInfoResolver = resolver };
        for (int i = probeOptions.Converters.Count - 1; i >= 0; i--)
        {
            if (probeOptions.Converters[i] is TolerantPolymorphicConverter)
            {
                probeOptions.Converters.RemoveAt(i);
            }
        }
        probeOptions.MakeReadOnly();
        return probeOptions;
    }

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
            IJsonTypeInfoResolver snapshot = SnapshotResolver(chainResolver);
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
        options.Converters.Insert(0, new TolerantPolymorphicConverter(resolver, options));
        options.TypeInfoResolver = resolver;
    }

    [DynamicDependency(
        DynamicallyAccessedMemberTypes.NonPublicFields,
        "System.Text.Json.Serialization.Metadata.JsonTypeInfoResolverWithAddedModifiers",
        "System.Text.Json")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The System.Text.Json resolver wrapper's private fields are explicitly preserved for resolver snapshotting.")]
    [SuppressMessage("SonarAnalyzer.CSharp", "S3011", Justification = "The private fields belong to System.Text.Json resolver wrappers and are read only to snapshot their public resolver behavior.")]
    private static IJsonTypeInfoResolver SnapshotResolver(IJsonTypeInfoResolver resolver, IReadOnlyList<IJsonTypeInfoResolver>? followingResolvers = null)
    {
        if (resolver is TolerantTypeInfoResolver tolerantResolver)
        {
            return followingResolvers is { Count: > 0 }
                ? tolerantResolver.Snapshot(followingResolvers)
                : tolerantResolver.Snapshot();
        }

        Type resolverType = resolver.GetType();
        FieldInfo[] fields = resolverType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        if (resolverType.FullName == "System.Text.Json.Serialization.Metadata.JsonTypeInfoResolverWithAddedModifiers")
        {
            // Rebuild the framework wrapper around a detached source so its modifier callbacks
            // stay intact without retaining an SDK resolver tied to caller-owned options.
            FieldInfo sourceField = fields.SingleOrDefault(field =>
                typeof(IJsonTypeInfoResolver).IsAssignableFrom(field.FieldType))
                ?? throw new InvalidOperationException($"Could not locate the wrapped resolver in '{resolverType.FullName}'.");
            FieldInfo modifiersField = fields.SingleOrDefault(field =>
                field.FieldType == typeof(Action<JsonTypeInfo>[]))
                ?? throw new InvalidOperationException($"Could not locate the metadata modifiers in '{resolverType.FullName}'.");

            IJsonTypeInfoResolver source = (IJsonTypeInfoResolver)sourceField.GetValue(resolver)!;
            Action<JsonTypeInfo>[] modifiers = (Action<JsonTypeInfo>[])modifiersField.GetValue(resolver)!;
            IJsonTypeInfoResolver snapshot = SnapshotResolver(source);
            foreach (Action<JsonTypeInfo> modifier in modifiers)
            {
                snapshot = snapshot.WithAddedModifier(modifier);
            }
            return snapshot;
        }

        if (resolverType.Assembly == typeof(JsonTypeInfoResolver).Assembly &&
            fields.FirstOrDefault(field => typeof(IList<IJsonTypeInfoResolver>).IsAssignableFrom(field.FieldType))
                ?.GetValue(resolver) is IList<IJsonTypeInfoResolver> resolverList)
        {
            List<IJsonTypeInfoResolver> snapshots = [];
            for (int index = 0; index < resolverList.Count; index++)
            {
                if (resolverList[index] is TolerantTypeInfoResolver nestedTolerantResolver)
                {
                    // A tolerant resolver incorporates later chain entries before its fallback
                    // resolvers. Fold those entries into its snapshot rather than listing them twice.
                    IJsonTypeInfoResolver[] followingSnapshots = resolverList
                        .Skip(index + 1)
                        .Select(nextResolver => SnapshotResolver(nextResolver))
                        .ToArray();
                    snapshots.Add(nestedTolerantResolver.Snapshot(followingSnapshots));
                    break;
                }

                IJsonTypeInfoResolver[] following = resolverList.Skip(index + 1).ToArray();
                snapshots.Add(SnapshotResolver(resolverList[index], following));
            }
            return JsonTypeInfoResolver.Combine(snapshots.ToArray());
        }

        return resolver;
    }

    internal static bool IsTolerantResolver(IJsonTypeInfoResolver resolver) =>
        resolver is TolerantTypeInfoResolver;

    public override bool CanConvert(Type typeToConvert) =>
        _supportedTypes.GetOrAdd((typeToConvert, TracksReferenceState(configuredOptions)), key =>
        {
            JsonTypeInfo? info = resolver.GetTypeInfo(key.Type, _probeOptions.Value);
            return info?.PolymorphismOptions is not null ||
                (key.PreserveReferences && info?.Kind is JsonTypeInfoKind.Object or JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary);
        });

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

        HashSet<Type> dictionaryKeyTypes = [];
        CollectDictionaryKeyTypes(underlyingOptions.GetTypeInfo(typeToConvert), underlyingOptions, dictionaryKeyTypes, []);
        JsonSerializerOptions nestedOptions = new(underlyingOptions)
        {
            TypeInfoResolver = underlyingOptions.TypeInfoResolver!.WithAddedModifier(info =>
            {
                if (info.Kind != JsonTypeInfoKind.Object)
                {
                    return;
                }

                foreach (JsonPropertyInfo property in info.Properties.Where(property =>
                    property.CustomConverter is not null && !IsScalarConverter(property.CustomConverter)))
                {
                    property.CustomConverter = new PropertyConverter(property, underlyingOptions);
                }
            })
        };
        nestedOptions.Converters.Insert(0, new CustomConverterForwarder(underlyingOptions, options, dictionaryKeyTypes));
        nestedOptions.MakeReadOnly();
        return new Converter(typeToConvert, nestedOptions);
    }

    private static bool IsScalarConverter(JsonConverter converter) =>
        converter is CidConverter or AtUriConverter or AtIdentifierConverter or DidConverter or HandleConverter or
            NsidConverter or RecordKeyConverter or TimestampIdentifierConverter or ByteConverter;

    private static int GetMaxDepth(JsonSerializerOptions options) => options.MaxDepth == 0 ? 64 : options.MaxDepth;

    private static bool PreservesReferences(JsonSerializerOptions options) =>
        options.ReferenceHandler is not null && options.ReferenceHandler != ReferenceHandler.IgnoreCycles;

    private static bool TracksReferenceState(JsonSerializerOptions options) =>
        PreservesReferences(options) || options.ReferenceHandler == ReferenceHandler.IgnoreCycles;

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Resolver-provided collection metadata preserves generic interface identities and arguments. No interface members are inspected or invoked.")]
    private static void CollectDictionaryKeyTypes(JsonTypeInfo info, JsonSerializerOptions options, HashSet<Type> keyTypes, HashSet<Type> visited)
    {
        if (!visited.Add(info.Type))
        {
            return;
        }

        if (info.PolymorphismOptions is JsonPolymorphismOptions polymorphism)
        {
            foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
            {
                CollectDictionaryKeyTypes(options.GetTypeInfo(derived.DerivedType), options, keyTypes, visited);
            }
        }

        if (info.Kind == JsonTypeInfoKind.Object)
        {
            foreach (JsonPropertyInfo property in info.Properties)
            {
                if (!property.IsExtensionData && (property.Get is not null || property.Set is not null) && property.CustomConverter is null)
                {
                    CollectDictionaryKeyTypes(options.GetTypeInfo(property.PropertyType), options, keyTypes, visited);
                }
            }
        }
        else if (info.Kind is JsonTypeInfoKind.Dictionary or JsonTypeInfoKind.Enumerable)
        {
            if (info.Type.IsArray)
            {
                CollectDictionaryKeyTypes(options.GetTypeInfo(info.Type.GetElementType()!), options, keyTypes, visited);
                return;
            }

            foreach (Type collectionType in info.Type.GetInterfaces().Prepend(info.Type))
            {
                if (!collectionType.IsGenericType)
                {
                    continue;
                }
                Type definition = collectionType.GetGenericTypeDefinition();
                if (info.Kind == JsonTypeInfoKind.Dictionary &&
                    (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>)))
                {
                    keyTypes.Add(collectionType.GenericTypeArguments[0]);
                    CollectDictionaryKeyTypes(options.GetTypeInfo(collectionType.GenericTypeArguments[1]), options, keyTypes, visited);
                    break;
                }
                if (info.Kind == JsonTypeInfoKind.Enumerable && definition == typeof(IEnumerable<>))
                {
                    CollectDictionaryKeyTypes(options.GetTypeInfo(collectionType.GenericTypeArguments[0]), options, keyTypes, visited);
                    break;
                }
            }
        }
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
            // The synthetic holder adds one level without changing the converter's depth budget.
            int maxDepth = GetMaxDepth(underlyingOptions);
            JsonSerializerOptions bridgeOptions = new(underlyingOptions)
            {
                MaxDepth = maxDepth < int.MaxValue ? maxDepth + 1 : maxDepth
            };
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
            using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { MaxDepth = _typeInfo.Options.MaxDepth }))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("value");
                writer.WriteRawValue(document.RootElement.GetRawText(), skipInputValidation: true);
                writer.WriteEndObject();
            }

            return JsonSerializer.Deserialize(buffer.WrittenSpan, _typeInfo)!.Value;
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(new PropertyValue { Value = value }, _typeInfo);
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = _typeInfo.Options.MaxDepth });
            document.RootElement.GetProperty("value").WriteTo(writer);
        }
    }

    private sealed class PropertyValue
    {
        public object? Value { get; set; }
    }

    // Opaque converters have no property contracts to traverse. Forward them to the SDK options so
    // any nested records they deserialize themselves still receive tolerant handling.
    private sealed class CustomConverterForwarder(JsonSerializerOptions probeOptions, JsonSerializerOptions sdkOptions, HashSet<Type> dictionaryKeyTypes) : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            Type valueType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
            return !valueType.IsEnum && !dictionaryKeyTypes.Contains(typeToConvert) &&
                GetConverter(typeToConvert) is JsonConverter converter &&
                !IsScalarConverter(converter) &&
                !HasCustomPropertyNameConversion(converter) &&
                converter.GetType().Assembly != typeof(JsonSerializer).Assembly;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The converter is instantiated by the configured resolver or converter factory, and virtual property-name conversion overrides remain available for its runtime type.")]
        private static bool HasCustomPropertyNameConversion(JsonConverter converter)
        {
            Type converterType = converter.GetType();
            MethodInfo? read = converterType.GetMethod(nameof(JsonConverter<object>.ReadAsPropertyName));
            MethodInfo? write = converterType.GetMethod(nameof(JsonConverter<object>.WriteAsPropertyName));
            return (read is not null && read.DeclaringType?.Assembly != typeof(JsonSerializer).Assembly) ||
                (write is not null && write.DeclaringType?.Assembly != typeof(JsonSerializer).Assembly);
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
        private readonly AsyncLocal<bool> _resolvingDecorator = new();
        private IJsonTypeInfoResolver? _combinedResolver;
        private IJsonTypeInfoResolver[]? _chainSnapshot;

        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            if (_resolvingDecorator.Value)
            {
                return null;
            }

            if (sdkOptions is not null)
            {
                IList<IJsonTypeInfoResolver> chain = sdkOptions.TypeInfoResolverChain;
                if (!chain.Any(resolver => ReferenceEquals(resolver, this)))
                {
                    foreach (IJsonTypeInfoResolver decorator in chain)
                    {
                        // The built-in modifier wrapper can retain the resolver chain that this
                        // options instance replaced, including resolvers appended before wrapping.
                        if (decorator.GetType().FullName != "System.Text.Json.Serialization.Metadata.JsonTypeInfoResolverWithAddedModifiers")
                        {
                            continue;
                        }

                        // Ask the decorator for its captured resolver chain, but stop its inner
                        // SDK resolver from re-entering the same decorator.
                        JsonTypeInfo? decoratedInfo;
                        _resolvingDecorator.Value = true;
                        try
                        {
                            decoratedInfo = decorator.GetTypeInfo(type, options);
                        }
                        finally
                        {
                            _resolvingDecorator.Value = false;
                        }

                        if (decoratedInfo is not null)
                        {
                            return ApplyCustomConverterPolymorphismWorkaround(decoratedInfo);
                        }
                    }
                }
            }

            JsonTypeInfo? info = GetCombinedResolver().GetTypeInfo(type, options);
            return info is null ? null : ApplyCustomConverterPolymorphismWorkaround(info);
        }

        public TolerantTypeInfoResolver Snapshot() => new(null, [GetCombinedResolver()]);

        public TolerantTypeInfoResolver Snapshot(IReadOnlyList<IJsonTypeInfoResolver> followingResolvers)
        {
            List<IJsonTypeInfoResolver> resolvers = [.. followingResolvers];
            resolvers.AddRange(initialResolvers.Where(resolver => !resolvers.Contains(resolver)));
            return new(null, [JsonTypeInfoResolver.Combine(resolvers.ToArray())]);
        }

        private IJsonTypeInfoResolver GetCombinedResolver()
        {
            lock (_resolverLock)
            {
                if (sdkOptions is null)
                {
                    return _combinedResolver ??= JsonTypeInfoResolver.Combine(initialResolvers);
                }

                IList<IJsonTypeInfoResolver> chain = sdkOptions.TypeInfoResolverChain;
                if (!chain.Any(resolver => ReferenceEquals(resolver, this)))
                {
                    // A decorator may call back into this resolver. Retain the last direct-chain
                    // snapshot rather than adding that decorator to our own resolution path.
                    return _combinedResolver ??= JsonTypeInfoResolver.Combine(initialResolvers);
                }
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
        public override bool HandleNull => true;

        private static JsonTypeInfo UnwrapNullable(JsonTypeInfo info, JsonSerializerOptions options)
        {
            Type? underlyingType = Nullable.GetUnderlyingType(info.Type);
            return underlyingType is not null && info.Converter.GetType().Assembly == typeof(JsonSerializer).Assembly
                ? options.GetTypeInfo(underlyingType)
                : info;
        }

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
#if NET10_0_OR_GREATER
            if (!underlyingOptions.AllowDuplicateProperties)
            {
                // The DOM retains duplicates even though this normalization may rewrite property names.
                ValidateNoDuplicateProperties(element);
            }
#endif
            // Reserve aliases against the complete payload and every reachable contract before rewriting.
            NormalizationState state = new(element);
            ReserveContractNames(element, underlyingOptions.GetTypeInfo(type), state);

            ArrayBufferWriter<byte> buffer = new();
            bool normalized;
            using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { MaxDepth = GetMaxDepth(underlyingOptions) }))
            {
                normalized = WriteNormalized(writer, element, underlyingOptions.GetTypeInfo(type), state);
            }
            if (!normalized)
            {
                // The probe can conservatively request a DOM for nested contracts; avoid reparsing its
                // output when the writer ultimately found nothing to change.
                return JsonSerializer.Deserialize(element, underlyingOptions.GetTypeInfo(type));
            }

            // Renamed properties now look like ordinary unknown fields to System.Text.Json, allowing
            // its normal extension-data handling to capture them. Restore aliases per object afterward.
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
                        // Restore only aliases found on this instance; nested objects may reuse the
                        // same alias for different source names.
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

#if NET10_0_OR_GREATER
        private static void ValidateNoDuplicateProperties(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                HashSet<string> propertyNames = new(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!propertyNames.Add(property.Name))
                    {
                        throw new JsonException($"Duplicate JSON property '{property.Name}'.");
                    }
                    ValidateNoDuplicateProperties(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    ValidateNoDuplicateProperties(item);
                }
            }
        }
#endif

        private sealed class NormalizationState
        {
            // Check aliases against the comparer behaviors supported by extension-data dictionaries.
            private readonly HashSet<string> _propertyNames = new(StringComparer.Ordinal);
            private readonly HashSet<string> _culturePropertyNames = new(StringComparer.CurrentCultureIgnoreCase);
            private readonly HashSet<string> _invariantPropertyNames = new(StringComparer.InvariantCultureIgnoreCase);
            private long _nextName;

            internal NormalizationState(JsonElement element)
            {
                CollectPropertyNames(element);
            }

            internal Dictionary<string, RenamedProperty> RenamedNames { get; } = new(StringComparer.Ordinal);

            internal void ReserveContractNames(JsonTypeInfo info)
            {
                // Temporary aliases must not shadow real input fields or names in the selected contract.
                foreach (JsonPropertyInfo property in info.Properties)
                {
                    ReserveName(property.Name);
                }

                if (info.PolymorphismOptions is JsonPolymorphismOptions polymorphism)
                {
                    ReserveName(polymorphism.TypeDiscriminatorPropertyName);
                }
            }

            internal string Rename(string originalName)
            {
                string temporaryName;
                do
                {
                    // A payload may already contain a field using an alias-looking name.
                    temporaryName = "__idunno_metadata_" + _nextName++.ToString(CultureInfo.InvariantCulture);
                }
                while (!ReserveName(temporaryName));

                RenamedNames.Add(temporaryName, new(originalName, RenamedNames.Count));
                return temporaryName;
            }

            private bool ReserveName(string name)
            {
                bool ordinal = _propertyNames.Add(name);
                bool culture = _culturePropertyNames.Add(name);
                bool invariant = _invariantPropertyNames.Add(name);
                return ordinal && culture && invariant;
            }

            private void CollectPropertyNames(JsonElement element)
            {
                // Scan all input names, not just modeled fields: even an opaque extension value can
                // already contain a string that resembles an alias generated for another object.
                if (element.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        ReserveName(property.Name);
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

        private sealed record RenamedProperty(string OriginalName, int Order);

        private static void RestoreNames<T>(IDictionary<string, T> data, Dictionary<string, RenamedProperty> renamedNames)
        {
            // Inspect only this object's keys; sort matching aliases by their source occurrence.
            // This preserves last-value-wins behavior when multiple unknown fields map to one key.
            KeyValuePair<string, RenamedProperty>[] aliases = data.Keys
                .Where(renamedNames.ContainsKey)
                .Select(name => new KeyValuePair<string, RenamedProperty>(name, renamedNames[name]))
                .OrderBy(pair => pair.Value.Order)
                .ToArray();
            foreach ((string temporaryName, RenamedProperty property) in aliases)
            {
                if (data.Remove(temporaryName, out T? item))
                {
                    data[property.OriginalName] = item;
                }
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
            info = UnwrapNullable(info, underlyingOptions);
            JsonPolymorphismOptions? polymorphicContract = info.PolymorphismOptions;
            if (reader.TokenType == JsonTokenType.StartObject && polymorphicContract is not null)
            {
                // Resolve the concrete contract before testing its kind; interface contracts can
                // select a collection whose `$values` envelope needs recursive normalization.
                Utf8JsonReader discriminatorReader = reader;
                while (discriminatorReader.Read() && discriminatorReader.TokenType != JsonTokenType.EndObject)
                {
                    bool isDiscriminator = discriminatorReader.ValueTextEquals(polymorphicContract.TypeDiscriminatorPropertyName);
                    discriminatorReader.Read();
                    if (isDiscriminator)
                    {
                        foreach (JsonDerivedType derived in polymorphicContract.DerivedTypes)
                        {
                            if ((derived.TypeDiscriminator is string text &&
                                    discriminatorReader.TokenType == JsonTokenType.String &&
                                    discriminatorReader.ValueTextEquals(text)) ||
                                (derived.TypeDiscriminator is int number &&
                                    discriminatorReader.TokenType == JsonTokenType.Number &&
                                    discriminatorReader.TryGetInt32(out int value) && value == number))
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

            if ((TracksReferenceState(underlyingOptions) || polymorphicContract is not null) &&
                reader.TokenType == JsonTokenType.StartObject && info.Kind == JsonTypeInfoKind.Enumerable)
            {
                // Polymorphic and reference-preserving collections use an object envelope around
                // the array. `$values` is always part of that envelope; `$id` and `$ref` depend on
                // reference preservation being enabled.
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    bool isDiscriminator = polymorphicContract is not null &&
                        reader.ValueTextEquals(polymorphicContract.TypeDiscriminatorPropertyName);
                    bool isValues = reader.ValueTextEquals("$values");
                    bool dollarName = reader.HasValueSequence || reader.ValueIsEscaped
                        ? reader.GetString()!.StartsWith('$')
                        : reader.ValueSpan.StartsWith("$"u8);
                    bool knownReferenceMetadata = isValues || PreservesReferences(underlyingOptions) &&
                        (reader.ValueTextEquals("$id") || reader.ValueTextEquals("$ref") || reader.ValueTextEquals("$values"));
                    reader.Read();
                    if (dollarName && !isDiscriminator && !knownReferenceMetadata)
                    {
                        return true;
                    }
                    if (isValues && NeedsNormalization(ref reader, info))
                    {
                        return true;
                    }
                    if (!isValues)
                    {
                        SkipValue(ref reader);
                    }
                }
                return false;
            }

            if (reader.TokenType == JsonTokenType.StartArray && info.Kind == JsonTypeInfoKind.Enumerable &&
                GetElementType(info) is Type elementType)
            {
                // Array elements are inspected against their declared type so nested polymorphic
                // objects are found without materializing the array.
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
                // Dictionary keys are property names, not object members; only recurse into values.
                JsonTypeInfo valueInfo = underlyingOptions.GetTypeInfo(valueType);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    bool isDiscriminator = polymorphicContract is not null &&
                        reader.ValueTextEquals(polymorphicContract.TypeDiscriminatorPropertyName);
                    bool knownReferenceMetadata = PreservesReferences(underlyingOptions) &&
                        (reader.ValueTextEquals("$id") || reader.ValueTextEquals("$ref"));
                    bool dollarName = reader.HasValueSequence || reader.ValueIsEscaped
                        ? reader.GetString()!.StartsWith('$')
                        : reader.ValueSpan.StartsWith("$"u8);
                    if (dollarName && !isDiscriminator && !knownReferenceMetadata &&
                        (polymorphicContract is not null || PreservesReferences(underlyingOptions)))
                    {
                        return true;
                    }

                    reader.Read();
                    if (!isDiscriminator && !knownReferenceMetadata && NeedsNormalization(ref reader, valueInfo))
                    {
                        return true;
                    }
                }
                return false;
            }

            if (reader.TokenType != JsonTokenType.StartObject || info.Kind != JsonTypeInfoKind.Object)
            {
                // Scalar and opaque custom-converter payloads are outside the contracts this walker
                // can safely inspect.
                SkipValue(ref reader);
                return false;
            }

            JsonPolymorphismOptions? polymorphism = polymorphicContract;
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
                    if (!candidate.IsExtensionData && reader.ValueTextEquals(candidate.Name))
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
                        if (!candidate.IsExtensionData && string.Equals(name, candidate.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            contract = candidate;
                            break;
                        }
                    }
                }

                bool dollarName = reader.HasValueSequence || reader.ValueIsEscaped
                    ? reader.GetString()!.StartsWith('$')
                    : reader.ValueSpan.StartsWith("$"u8);
                // Preserve protocol metadata, but normalize unrecognized dollar-prefixed fields
                // even on reference-tracked objects that do not declare polymorphism.
                bool knownReferenceMetadata = PreservesReferences(underlyingOptions) &&
                    (reader.ValueTextEquals("$id") || reader.ValueTextEquals("$ref") || reader.ValueTextEquals("$values"));
                if ((polymorphism is not null || PreservesReferences(underlyingOptions)) &&
                    contract is null && dollarName &&
                    !(polymorphism is not null && reader.ValueTextEquals(polymorphism.TypeDiscriminatorPropertyName)) &&
                    !knownReferenceMetadata)
                {
                    return true;
                }

                reader.Read();
                if (contract is not null && (contract.Get is not null || contract.Set is not null) && contract.CustomConverter is null)
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

        private void ReserveContractNames(JsonElement element, JsonTypeInfo info, NormalizationState state)
        {
            info = UnwrapNullable(info, underlyingOptions);
            if (element.ValueKind == JsonValueKind.Object &&
                TryGetDerivedTypeInfo(element, info) is JsonTypeInfo derivedInfo)
            {
                info = derivedInfo;
            }

            if (element.ValueKind == JsonValueKind.Object && info.Kind == JsonTypeInfoKind.Enumerable &&
                element.TryGetProperty("$values", out JsonElement values))
            {
                // The envelope belongs to the selected collection contract; reserve names from its items.
                ReserveContractNames(values, info, state);
                return;
            }

            if (element.ValueKind == JsonValueKind.Array && info.Kind == JsonTypeInfoKind.Enumerable &&
                GetElementType(info) is Type elementType)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    ReserveContractNames(item, underlyingOptions.GetTypeInfo(elementType), state);
                }
                return;
            }

            if (element.ValueKind == JsonValueKind.Object && info.Kind == JsonTypeInfoKind.Dictionary &&
                GetElementType(info) is Type valueType)
            {
                JsonPolymorphismOptions? dictionaryPolymorphism = info.PolymorphismOptions;
                if (dictionaryPolymorphism is not null &&
                    element.TryGetProperty(dictionaryPolymorphism.TypeDiscriminatorPropertyName, out JsonElement dictionaryDiscriminator))
                {
                    foreach (JsonDerivedType derived in dictionaryPolymorphism.DerivedTypes)
                    {
                        if ((derived.TypeDiscriminator is string text &&
                                dictionaryDiscriminator.ValueKind == JsonValueKind.String &&
                                dictionaryDiscriminator.GetString() == text) ||
                            (derived.TypeDiscriminator is int number &&
                                dictionaryDiscriminator.ValueKind == JsonValueKind.Number &&
                                dictionaryDiscriminator.TryGetInt32(out int value) && value == number))
                        {
                            info = underlyingOptions.GetTypeInfo(derived.DerivedType);
                            valueType = GetElementType(info) ?? valueType;
                            break;
                        }
                    }
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Name != dictionaryPolymorphism?.TypeDiscriminatorPropertyName &&
                        !(PreservesReferences(underlyingOptions) && property.Name is "$id" or "$ref"))
                    {
                        ReserveContractNames(property.Value, underlyingOptions.GetTypeInfo(valueType), state);
                    }
                }
                return;
            }

            if (element.ValueKind != JsonValueKind.Object || info.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }

            // Include base and selected-derived names so an alias cannot shadow either side of dispatch.
            state.ReserveContractNames(info);
            JsonPolymorphismOptions? polymorphism = info.PolymorphismOptions;
            if (polymorphism is not null)
            {
                foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
                {
                    if (element.TryGetProperty(polymorphism.TypeDiscriminatorPropertyName, out JsonElement discriminator) &&
                        ((derived.TypeDiscriminator is string text && discriminator.ValueKind == JsonValueKind.String && discriminator.GetString() == text) ||
                        (derived.TypeDiscriminator is int number && discriminator.ValueKind == JsonValueKind.Number && discriminator.TryGetInt32(out int value) && value == number)))
                    {
                        info = underlyingOptions.GetTypeInfo(derived.DerivedType);
                        state.ReserveContractNames(info);
                        break;
                    }
                }

            }

            StringComparison comparison = underlyingOptions.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (JsonProperty property in element.EnumerateObject())
            {
                JsonPropertyInfo? contract = info.Properties.FirstOrDefault(candidate =>
                    !candidate.IsExtensionData &&
                    string.Equals(candidate.Name, property.Name, comparison));
                if (contract is not null && (contract.Get is not null || contract.Set is not null) && contract.CustomConverter is null)
                {
                    ReserveContractNames(property.Value, underlyingOptions.GetTypeInfo(contract.PropertyType), state);
                }
            }
        }

        private JsonTypeInfo? TryGetDerivedTypeInfo(JsonElement element, JsonTypeInfo info)
        {
            // Unknown or malformed discriminators remain the serializer's responsibility; only known
            // derived contracts can guide the normalizer's shape-specific traversal.
            JsonPolymorphismOptions? polymorphism = info.PolymorphismOptions;
            if (polymorphism is null ||
                !element.TryGetProperty(polymorphism.TypeDiscriminatorPropertyName, out JsonElement discriminator))
            {
                return null;
            }

            foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
            {
                if ((derived.TypeDiscriminator is string text &&
                        discriminator.ValueKind == JsonValueKind.String &&
                        discriminator.GetString() == text) ||
                    (derived.TypeDiscriminator is int number &&
                        discriminator.ValueKind == JsonValueKind.Number &&
                        discriminator.TryGetInt32(out int value) && value == number))
                {
                    return underlyingOptions.GetTypeInfo(derived.DerivedType);
                }
            }

            return null;
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2075",
            Justification = "Ignored member names are queried only when normalizing a payload; source-generated public contracts preserve their declared members, including JsonIgnore attributes.")]
        private HashSet<string> GetIgnoredPropertyNames(JsonTypeInfo info)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            // Ignored members are omitted from the JSON contract, but their names must not be
            // mistaken for extension data when normalization rewrites the containing object.
            foreach (PropertyInfo property in info.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always })
                {
                    continue;
                }

                string name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                    underlyingOptions.PropertyNamingPolicy?.ConvertName(property.Name) ??
                    property.Name;
                names.Add(name);
            }

            return names;
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
            info = UnwrapNullable(info, underlyingOptions);
            JsonPolymorphismOptions? polymorphicContract = info.PolymorphismOptions;
            if (element.ValueKind == JsonValueKind.Object &&
                TryGetDerivedTypeInfo(element, info) is JsonTypeInfo derivedInfo)
            {
                info = derivedInfo;
            }

            if ((TracksReferenceState(underlyingOptions) || polymorphicContract is not null) &&
                element.ValueKind == JsonValueKind.Object && info.Kind == JsonTypeInfoKind.Enumerable)
            {
                // Normalize only unsupported envelope fields; keep the discriminator and required array payload.
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    bool isDiscriminator = polymorphicContract is not null &&
                        property.Name == polymorphicContract.TypeDiscriminatorPropertyName;
                    bool isValues = property.NameEquals("$values");
                    bool dollarName = property.Name.StartsWith('$');
                    bool knownReferenceMetadata = isValues || PreservesReferences(underlyingOptions) &&
                        property.Name is "$id" or "$ref" or "$values";
                    if (dollarName && !isDiscriminator && !knownReferenceMetadata)
                    {
                        normalized = true;
                        continue;
                    }

                    writer.WritePropertyName(property.Name);
                    if (isValues)
                    {
                        normalized |= WriteNormalized(writer, property.Value, info, state);
                    }
                    else
                    {
                        writer.WriteRawValue(property.Value.GetRawText(), skipInputValidation: true);
                    }
                }
                writer.WriteEndObject();
                return normalized;
            }

            if (element.ValueKind == JsonValueKind.Object && info.Kind == JsonTypeInfoKind.Dictionary && GetElementType(info) is Type valueType)
            {
                // A polymorphic dictionary keeps the discriminator outside its entries; its selected
                // contract can also change the value type used for recursive traversal.
                JsonPolymorphismOptions? dictionaryPolymorphism = polymorphicContract;
                if (dictionaryPolymorphism is not null &&
                    element.TryGetProperty(dictionaryPolymorphism.TypeDiscriminatorPropertyName, out JsonElement dictionaryDiscriminator))
                {
                    foreach (JsonDerivedType derived in dictionaryPolymorphism.DerivedTypes)
                    {
                        if ((derived.TypeDiscriminator is string text &&
                                dictionaryDiscriminator.ValueKind == JsonValueKind.String &&
                                dictionaryDiscriminator.GetString() == text) ||
                            (derived.TypeDiscriminator is int number &&
                                dictionaryDiscriminator.ValueKind == JsonValueKind.Number &&
                                dictionaryDiscriminator.TryGetInt32(out int value) && value == number))
                        {
                            info = underlyingOptions.GetTypeInfo(derived.DerivedType);
                            break;
                        }
                    }
                }

                valueType = GetElementType(info) ?? valueType;
                JsonTypeInfo valueInfo = underlyingOptions.GetTypeInfo(valueType);
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    bool isDiscriminator = dictionaryPolymorphism is not null &&
                        property.Name == dictionaryPolymorphism.TypeDiscriminatorPropertyName;
                    bool knownReferenceMetadata = PreservesReferences(underlyingOptions) &&
                        property.Name is "$id" or "$ref";
                    bool unknownMetadata = (dictionaryPolymorphism is not null || PreservesReferences(underlyingOptions)) &&
                        property.Name.StartsWith('$') && !isDiscriminator && !knownReferenceMetadata;
                    if (unknownMetadata)
                    {
                        normalized = true;
                        continue;
                    }

                    writer.WritePropertyName(property.Name);
                    if (!isDiscriminator && !knownReferenceMetadata)
                    {
                        normalized |= WriteNormalized(writer, property.Value, valueInfo, state);
                    }
                    else
                    {
                        writer.WriteRawValue(property.Value.GetRawText(), skipInputValidation: true);
                    }
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
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                return false;
            }

            JsonPolymorphismOptions? polymorphism = polymorphicContract;
            // Select the concrete contract before deciding which dollar-prefixed names are unknown.
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
            HashSet<string> ignoredNames = GetIgnoredPropertyNames(info);
            bool hasExtensionData = info.Properties.Any(property => property.IsExtensionData);
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject())
            {
                JsonPropertyInfo? contract = info.Properties.FirstOrDefault(candidate =>
                    !candidate.IsExtensionData &&
                    string.Equals(candidate.Name, property.Name, comparison));
                bool ignoredProperty = contract is null &&
                    ignoredNames.Contains(property.Name, comparison == StringComparison.OrdinalIgnoreCase
                        ? StringComparer.OrdinalIgnoreCase
                        : StringComparer.Ordinal);

                bool unknownMetadata = (polymorphism is not null || PreservesReferences(underlyingOptions)) &&
                    property.Name.StartsWith('$') &&
                    property.Name != polymorphism?.TypeDiscriminatorPropertyName &&
                    contract is null && !ignoredProperty &&
                    !(PreservesReferences(underlyingOptions) && property.Name is "$id" or "$ref" or "$values");

                // Keep ordinary fields untouched; only unknown protocol metadata needs an alias so
                // System.Text.Json will accept and route it to extension data.
                normalized |= unknownMetadata;
                if (unknownMetadata && !hasExtensionData)
                {
                    continue;
                }

                bool extensionDataProperty = hasExtensionData &&
                    contract is null &&
                    !ignoredProperty &&
                    property.Name != polymorphism?.TypeDiscriminatorPropertyName &&
                    !(PreservesReferences(underlyingOptions) && property.Name is "$id" or "$ref" or "$values");
                writer.WritePropertyName(extensionDataProperty ? state.Rename(property.Name) : property.Name);
                if (contract is not null && (contract.Get is not null || contract.Set is not null) && contract.CustomConverter is null)
                {
                    normalized |= WriteNormalized(writer, property.Value, underlyingOptions.GetTypeInfo(contract.PropertyType), state);
                }
                else
                {
                    writer.WriteRawValue(property.Value.GetRawText(), skipInputValidation: true);
                }
            }
            writer.WriteEndObject();
            return normalized;
        }
    }
}
