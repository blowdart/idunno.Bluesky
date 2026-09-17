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
// The trimming and AOT suppressions must be applied to the type rather than to s_options. The initializer for a
// static field is emitted into the generated static constructor, and suppression lookup walks from the member the
// IL is attributed to out to its declaring type, so a suppression on the field itself is never consulted by ILLink.
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "All the AtProto Types are captured in source gen")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "All the AtProto Types are captured in source gen")]
public static class BlueskyJsonSerializerOptions
{
    [SuppressMessage("Style", "IDE0032:Use auto property", Justification = "Won't work with the suppress message attributes")]
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerOptions.Web)
    {
        TypeInfoResolver = SourceGenerationContext.Default,
        AllowOutOfOrderMetadataProperties = true
    };

    /// <summary>
    /// Creates a new set of <see cref="JsonSerializerOptions"/> for Bluesky types.
    /// </summary>
    public static JsonSerializerOptions Options => AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(s_options);

    /// <summary>
    /// Gets the default <see cref="IJsonTypeInfoResolver"/> for Bluesky types.
    /// </summary>
    public static IJsonTypeInfoResolver TypeInfoResolver => Options.TypeInfoResolver!;
}