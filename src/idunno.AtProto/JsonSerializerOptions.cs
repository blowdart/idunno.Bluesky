// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.
//
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace idunno.AtProto;

public partial class AtProtoServer
{
    /// <summary>
    /// Gets a shared, read only <see cref="JsonSerializerOptions"/> configured to use Json source generation for AtProto classes.
    /// </summary>
    /// <remarks>
    /// <para>The instance is shared so the type metadata it resolves is cached across calls, rather than rebuilt for every
    /// request. It is read only, so use <see cref="CreateAtProtoJsonSerializerOptions"/> for options which need to be changed.</para>
    /// </remarks>
    internal static JsonSerializerOptions AtProtoJsonSerializerOptions { get; } = MakeReadOnly(CreateAtProtoJsonSerializerOptions());

    /// <summary>
    /// Gets shared <see cref="JsonSerializerOptions"/> with reflection-backed type resolution when available.
    /// </summary>
    /// <remarks>
    /// <para>The instance is shared so the reflection based type metadata it builds is cached across calls, rather than rebuilt
    /// for every record. It becomes read only when it is first used.</para>
    /// </remarks>
    internal static JsonSerializerOptions DefaultJsonSerializerOptionsWithNoTypeResolution { get; } = CreateDefaultJsonSerializerOptionsWithNoTypeResolution();

    /// <summary>
    /// Creates a new, mutable <see cref="JsonSerializerOptions"/> configured to use Json source generation for AtProto classes.
    /// </summary>
    /// <returns>A new <see cref="JsonSerializerOptions"/> configured to use Json source generation for AtProto classes.</returns>
    internal static JsonSerializerOptions CreateAtProtoJsonSerializerOptions(bool adaptContracts = true)
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IgnoreReadOnlyProperties = false,
            RespectNullableAnnotations = true,
            TypeInfoResolver = SourceGenerationContext.Default,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        };
        if (adaptContracts)
        {
            Json.TolerantPolymorphicConverter.Configure(options);
        }
        return options;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "This reflection-backed fallback is used only when reflection is enabled; AOT uses the AtProto source-generation context.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "This reflection-backed fallback is used only when reflection is enabled; AOT uses the AtProto source-generation context.")]
    private static JsonSerializerOptions CreateDefaultJsonSerializerOptionsWithNoTypeResolution()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IgnoreReadOnlyProperties = false,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            RespectNullableAnnotations = true,
            TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault
                ? new DefaultJsonTypeInfoResolver()
                : JsonTypeInfoResolver.Combine(SourceGenerationContext.Default)
        };
        Json.TolerantPolymorphicConverter.Configure(options);
        return options;
    }

    /// <summary>
    /// Gets an instance of <see cref="JsonSerializerOptions" /> with the specified <paramref name="jsonSerializerOptions"/> type resolver chained.
    /// </summary>
    /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to chain type resolution with.</param>
    /// <returns>An instance of <see cref="JsonSerializerOptions" /> with the type information resolvers chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jsonSerializerOptions"/> or its TypeInfoResolver is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The result uses AtProto defaults and the supplied resolver, not the supplied settings or converters.
    /// Call this helper after customizing a mutable options instance to restore SDK contract adaptation.</para>
    /// <para>The result remains mutable for compatibility. Arbitrary mutations are not intercepted; replacing or prepending
    /// resolvers can bypass adaptation again. Prefer <see cref="AtProto.AtProtoJsonSerializerOptions.Create(Action{JsonSerializerOptions})"/>
    /// to configure options before returning a finalized, read-only result.</para>
    /// </remarks>
    public static JsonSerializerOptions BuildChainedTypeInfoResolverJsonSerializerOptions(JsonSerializerOptions jsonSerializerOptions)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions.TypeInfoResolver);

        return BuildChainedTypeInfoResolverJsonSerializerOptions(jsonSerializerOptions.TypeInfoResolver);
    }

    /// <summary>
    /// Creates a new instance of <see cref="JsonSerializerOptions"/> with the specified <paramref name="jsonSerializerOptions"/> chained to the AtProto source generation resolver.
    /// </summary>
    /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to chain type resolution with.</param>
    /// <returns>An instance of <see cref="JsonSerializerOptions"/> with the type information resolvers chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jsonSerializerOptions"/> or any of its elements or their TypeInfoResolver is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The result uses AtProto defaults and the supplied resolvers, not the supplied settings or converters.
    /// It remains mutable for compatibility; arbitrary resolver replacement or prepending can bypass SDK contract adaptation.
    /// Use this helper after resolver customization, or prefer <see cref="AtProto.AtProtoJsonSerializerOptions.Create(Action{JsonSerializerOptions})"/>
    /// for a finalized, read-only result.</para>
    /// </remarks>
    public static JsonSerializerOptions BuildChainedTypeInfoResolverJsonSerializerOptions(params JsonSerializerOptions[] jsonSerializerOptions)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);

        JsonSerializerOptions result = CreateAtProtoJsonSerializerOptions(adaptContracts: false);

        foreach (JsonSerializerOptions options in jsonSerializerOptions)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(options.TypeInfoResolver);

            result.TypeInfoResolverChain.Insert(0, options.TypeInfoResolver);
        }

        Json.TolerantPolymorphicConverter.Configure(result);
        return result;
    }

    /// <summary>
    /// Gets an instance of <see cref="JsonSerializerOptions" /> with the specified <paramref name="jsonTypeInfoResolver"/> type resolver chained.
    /// </summary>
    /// <param name="jsonTypeInfoResolver">The type information resolver to insert into the type resolution chain.</param>
    /// <returns>An instance of <see cref="JsonSerializerOptions" /> with the type information resolvers chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jsonTypeInfoResolver"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The result remains mutable for compatibility. Arbitrary resolver mutations are not intercepted; replacing or
    /// prepending resolvers can bypass SDK contract adaptation. Call this helper after resolver customization, or prefer
    /// <see cref="AtProto.AtProtoJsonSerializerOptions.Create(Action{JsonSerializerOptions})"/> for a finalized, read-only result.</para>
    /// </remarks>
    public static JsonSerializerOptions BuildChainedTypeInfoResolverJsonSerializerOptions(IJsonTypeInfoResolver jsonTypeInfoResolver)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfoResolver);

        JsonSerializerOptions options = CreateAtProtoJsonSerializerOptions(adaptContracts: false);

        options.TypeInfoResolverChain.Insert(0, jsonTypeInfoResolver);

        Json.TolerantPolymorphicConverter.Configure(options);
        return options;
    }

    private static JsonSerializerOptions MakeReadOnly(JsonSerializerOptions options)
    {
        options.MakeReadOnly();
        return options;
    }
}