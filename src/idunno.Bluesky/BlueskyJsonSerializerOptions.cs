// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;

namespace idunno.Bluesky;

/// <summary>
/// Helper class to provide JsonSerializerOptions for Bluesky types.
/// </summary>
/// <remarks>
/// <para>Unknown JSON properties, including names beginning with <c>$</c>, are retained in extension data when the
/// selected contract supports it, and otherwise ignored. Type discriminator and required property validation are unchanged.</para>
/// </remarks>
// The trimming and AOT suppressions are applied to the type so that they cover every member which reaches the
// chaining call, wherever the compiler emits that IL. Suppression lookup walks from the member the IL is
// attributed to out to its declaring type, so a type level suppression is always consulted by ILLink.
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "All the AtProto Types are captured in source gen")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "All the AtProto Types are captured in source gen")]
public static class BlueskyJsonSerializerOptions
{
    private static readonly JsonSerializerOptions s_default = AtProto.Json.TolerantPolymorphicConverter.FinalizeOptions(CreateMutableOptions());

    /// <summary>
    /// Creates read-only JSON options for Bluesky types after applying caller configuration.
    /// </summary>
    /// <param name="configure">The configuration callback for the mutable options.</param>
    /// <returns>A new, read-only set of options with SDK contract adaptation around the configured resolvers.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The callback runs before SDK contract adaptation is installed. It may replace or prepend resolvers.
    /// The configured options are copied before finalization; the callback's instance remains mutable and is not used by the result.</para>
    /// <para>Resolver and converter instances are shared, not cloned; do not mutate their state after finalization.
    /// The callback's options do not have SDK contract adaptation installed and should not be used for serialization.</para>
    /// </remarks>
    public static JsonSerializerOptions Create(Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        JsonSerializerOptions options = CreateMutableOptions();
        configure(options);
        return AtProto.Json.TolerantPolymorphicConverter.FinalizeOptions(options);
    }

    /// <summary>
    /// Creates a new set of <see cref="JsonSerializerOptions"/> for Bluesky types.
    /// </summary>
    /// <remarks>
    /// <para>Each call returns a new, mutable instance, which must resolve its type metadata again the first time it is used.
    /// Use <see cref="Default"/> unless you need to change the options.</para>
    /// <para>Append custom resolvers to <see cref="JsonSerializerOptions.TypeInfoResolverChain"/> to keep the SDK's
    /// contract adaptation first. Use <see cref="AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(JsonSerializerOptions)"/>
    /// after replacing or prepending resolvers, including on copies made with the
    /// <see cref="JsonSerializerOptions.JsonSerializerOptions(JsonSerializerOptions)"/> constructor.</para>
    /// <para>Arbitrary mutations of returned options are not intercepted. Replacing or prepending resolvers can bypass
    /// adaptation. Prefer <see cref="Create(Action{JsonSerializerOptions})"/> to configure options before finalization.</para>
    /// </remarks>
    public static JsonSerializerOptions Options
    {
        get
        {
            JsonSerializerOptions options = CreateMutableOptions();
            AtProto.Json.TolerantPolymorphicConverter.Configure(options);
            return options;
        }
    }

    /// <summary>
    /// Gets a shared, read only set of <see cref="JsonSerializerOptions"/> for Bluesky types.
    /// </summary>
    /// <remarks>
    /// <para>The instance caches the type metadata it resolves, so it is cheaper than <see cref="Options"/> when serializing
    /// or deserializing repeatedly. It cannot be changed; use <see cref="Options"/> to get a copy which can.</para>
    /// </remarks>
    public static JsonSerializerOptions Default => s_default;

    /// <summary>
    /// Gets the default <see cref="IJsonTypeInfoResolver"/> for Bluesky types.
    /// </summary>
    public static IJsonTypeInfoResolver TypeInfoResolver => s_default.TypeInfoResolver!;

    private static JsonSerializerOptions CreateMutableOptions()
    {
        JsonSerializerOptions options = AtProtoServer.CreateAtProtoJsonSerializerOptions(adaptContracts: false);
        options.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);

        options.Converters.Add(new Json.PreferenceConverter());

        return options;
    }
}