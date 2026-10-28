// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace idunno.AtProto;

/// <summary>
/// Helper class to provide JsonSerializerOptions for AtProto types.
/// </summary>
/// <remarks>
/// <para>Unknown JSON properties, including names beginning with <c>$</c>, are retained in extension data when the
/// selected contract supports it, and otherwise ignored. Type discriminator and required property validation are unchanged.</para>
/// <para>When reference handling is enabled and async-enumerable values are serialized, use <see cref="CreateReadOptions"/>
/// for deserialization and <see cref="CreateWriteOptions"/> for serialization so each direction can use its own cached contracts.</para>
/// </remarks>
// The trimming and AOT suppressions must be applied to the type rather than to Default. Its backing field
// initializer is emitted into the static constructor, so ILLink does not consult suppressions on the property.
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "All the AtProto Types are captured in source gen")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "All the AtProto Types are captured in source gen")]
public static class AtProtoJsonSerializerOptions
{
    internal static JsonSerializerOptions CreateMutableOptions() => new(JsonSerializerOptions.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        TypeInfoResolver = SourceGenerationContext.Default,
    };

    /// <summary>
    /// Creates read-only JSON options for AtProto types after applying caller configuration.
    /// </summary>
    /// <param name="configure">The configuration callback for the mutable options.</param>
    /// <returns>A new, read-only set of options with SDK contract adaptation around the configured resolvers.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The callback runs before SDK contract adaptation is installed. It may replace or prepend resolvers.
    /// The configured options are copied before finalization; the callback's instance remains mutable and is not used by the result.</para>
    /// <para>Resolver and converter instances are shared, not cloned; do not mutate their state after finalization.
    /// The callback's options do not have SDK contract adaptation installed and should not be used for serialization.</para>
    /// <para>For reference-handled async-enumerable serialization, prefer <see cref="CreateReadOptions"/> and
    /// <see cref="CreateWriteOptions"/> to keep read and write contract selection independent.</para>
    /// </remarks>
    public static JsonSerializerOptions Create(Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        JsonSerializerOptions options = CreateMutableOptions();
        configure(options);
        return Json.TolerantPolymorphicConverter.FinalizeOptions(options);
    }

    /// <summary>
    /// Creates read-only JSON options for deserializing AtProto types with tolerant handling of unknown metadata.
    /// </summary>
    /// <param name="configure">The configuration callback for the mutable options.</param>
    /// <returns>A new, read-only set of options with SDK contract adaptation enabled for reads.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Use these options for deserialization. For serialization of async-enumerable roots or objects that contain
    /// async-enumerable properties with reference handling enabled, use <see cref="CreateWriteOptions"/> instead.
    /// System.Text.Json uses one cached contract for both directions, so the SDK exposes direction-specific options
    /// for this scenario.</para>
    /// </remarks>
    public static JsonSerializerOptions CreateReadOptions(Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        JsonSerializerOptions options = CreateMutableOptions();
        configure(options);
        return Json.TolerantPolymorphicConverter.FinalizeOptions(options, preserveAsyncEnumerableWritePath: false);
    }

    /// <summary>
    /// Creates read-only JSON options for serializing AtProto types through the built-in serializer contracts.
    /// </summary>
    /// <param name="configure">The configuration callback for the mutable options.</param>
    /// <returns>A new, read-only set of options without the SDK's tolerant read adapter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Use these options for serialization, especially when serializing async-enumerable values with reference
    /// handling. Apply matching settings and resolvers to both the read and write options when using them as a pair.</para>
    /// </remarks>
    public static JsonSerializerOptions CreateWriteOptions(Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        JsonSerializerOptions options = CreateMutableOptions();
        configure(options);
        return Json.TolerantPolymorphicConverter.FinalizeWriteOptions(options);
    }

    /// <summary>
    /// Creates a new set of <see cref="JsonSerializerOptions"/> for AtProto types.
    /// </summary>
    /// <remarks>
    /// <para>Each call returns a new, mutable instance, which must resolve its type metadata again the first time it is used.
    /// Use <see cref="Default"/> unless you need to change the options.</para>
    /// <para>Append custom resolvers to <see cref="JsonSerializerOptions.TypeInfoResolverChain"/> to preserve the SDK's
    /// contract adaptation. After replacing or prepending resolvers, or customizing a copy made with the
    /// <see cref="JsonSerializerOptions.JsonSerializerOptions(JsonSerializerOptions)"/> constructor, use
    /// <see cref="AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(JsonSerializerOptions)"/>
    /// to create options with SDK contract adaptation around the customized resolver.</para>
    /// <para>Arbitrary mutations of returned options are not intercepted. Replacing or prepending resolvers can bypass
    /// adaptation. Prefer <see cref="Create(Action{JsonSerializerOptions})"/> to configure options before finalization.</para>
    /// </remarks>
    public static JsonSerializerOptions Options
    {
        get
        {
            JsonSerializerOptions options = CreateMutableOptions();
            Json.TolerantPolymorphicConverter.Configure(options);
            return options;
        }
    }

    /// <summary>
    /// Gets a shared, read only set of <see cref="JsonSerializerOptions"/> for AtProto types.
    /// </summary>
    /// <remarks>
    /// <para>The instance caches the type metadata it resolves, so it is cheaper than <see cref="Options"/> when serializing
    /// or deserializing repeatedly. It cannot be changed; use <see cref="Options"/> to get a copy which can.</para>
    /// </remarks>
    public static JsonSerializerOptions Default { get; } = Json.TolerantPolymorphicConverter.FinalizeOptions(CreateMutableOptions());

    /// <summary>
    /// Gets the default <see cref="IJsonTypeInfoResolver"/> for AtProto types.
    /// </summary>
    public static IJsonTypeInfoResolver TypeInfoResolver => Default.TypeInfoResolver!;
}
