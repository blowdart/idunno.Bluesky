// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace idunno.AtProto;

/// <summary>
/// Options to configure JSON serialization settings for <see cref="Agent"/>
/// </summary>
public sealed class JsonOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonOptions"/> class.
    /// </summary>
    public JsonOptions() : this(adaptContracts: true)
    {
    }

    internal JsonOptions(bool adaptContracts)
    {
        if (adaptContracts)
        {
            Json.TolerantPolymorphicConverter.Configure(JsonSerializerOptions);
        }
    }

    internal JsonOptions(JsonSerializerOptions options)
    {
        JsonSerializerOptions = options;
    }

    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> used by <see cref="Agent"/> 
    /// </summary>
    /// <remarks>
    /// <para>Agents copy and finalize these options at construction, after caller configuration, without freezing this instance.
    /// Options exposed by an agent are read-only.</para>
    /// <para>Before agent construction this instance is mutable. Arbitrary mutations are not intercepted; replacing or prepending
    /// resolvers can bypass contract adaptation during direct serialization. Use
    /// <see cref="AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(JsonSerializerOptions)"/> after such mutations,
    /// or use <see cref="AtProtoJsonSerializerOptions.Create(Action{JsonSerializerOptions})"/> for direct serialization.</para>
    /// </remarks>
    public JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = false,
        RespectNullableAnnotations = true,
        // The JsonSerializerOptions.GetTypeInfo method is called directly and needs a defined resolver
        // setting the default resolver (reflection-based) but the user can overwrite it directly or by modifying
        // the TypeInfoResolverChain. Use JsonTypeInfoResolver.Combine() to produce an empty TypeInfoResolver.
        TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault ? CreateDefaultTypeResolver() : JsonTypeInfoResolver.Combine(SourceGenerationContext.Default),
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "Combined with TypeResolver above.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "Combined with TypeResolver above.")]
    private static DefaultJsonTypeInfoResolver CreateDefaultTypeResolver()
    {
        return new DefaultJsonTypeInfoResolver();
    }
}