// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.
//
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
    /// Gets a shared <see cref="JsonSerializerOptions"/> without a type resolver.
    /// </summary>
    /// <remarks>
    /// <para>The instance is shared so the reflection based type metadata it builds is cached across calls, rather than rebuilt
    /// for every record. It becomes read only when it is first used.</para>
    /// </remarks>
    internal static JsonSerializerOptions DefaultJsonSerializerOptionsWithNoTypeResolution { get; } = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = false,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        RespectNullableAnnotations = true
    };

    /// <summary>
    /// Creates a new, mutable <see cref="JsonSerializerOptions"/> configured to use Json source generation for AtProto classes.
    /// </summary>
    /// <returns>A new <see cref="JsonSerializerOptions"/> configured to use Json source generation for AtProto classes.</returns>
    internal static JsonSerializerOptions CreateAtProtoJsonSerializerOptions() => new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = false,
        RespectNullableAnnotations = true,
        TypeInfoResolver = SourceGenerationContext.Default,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    /// <summary>
    /// Gets an instance of <see cref="JsonSerializerOptions" /> with the specified <paramref name="jsonSerializerOptions"/> type resolver chained.
    /// </summary>
    /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to chain type resolution with.</param>
    /// <returns>An instance of <see cref="JsonSerializerOptions" /> with the type information resolvers chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jsonSerializerOptions"/> or its TypeInfoResolver is <see langword="null"/>.</exception>
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
    public static JsonSerializerOptions BuildChainedTypeInfoResolverJsonSerializerOptions(params JsonSerializerOptions[] jsonSerializerOptions)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);

        JsonSerializerOptions result = CreateAtProtoJsonSerializerOptions();

        foreach (JsonSerializerOptions options in jsonSerializerOptions)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(options.TypeInfoResolver);

            result.TypeInfoResolverChain.Insert(0, options.TypeInfoResolver);
        }

        return result;
    }

    /// <summary>
    /// Gets an instance of <see cref="JsonSerializerOptions" /> with the specified <paramref name="jsonTypeInfoResolver"/> type resolver chained.
    /// </summary>
    /// <param name="jsonTypeInfoResolver">The type information resolver to insert into the type resolution chain.</param>
    /// <returns>An instance of <see cref="JsonSerializerOptions" /> with the type information resolvers chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jsonTypeInfoResolver"/> is <see langword="null"/>.</exception>
    public static JsonSerializerOptions BuildChainedTypeInfoResolverJsonSerializerOptions(IJsonTypeInfoResolver jsonTypeInfoResolver)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfoResolver);

        JsonSerializerOptions options = CreateAtProtoJsonSerializerOptions();

        options.TypeInfoResolverChain.Insert(0, jsonTypeInfoResolver);

        return options;
    }

    private static JsonSerializerOptions MakeReadOnly(JsonSerializerOptions options)
    {
        options.MakeReadOnly();
        return options;
    }
}