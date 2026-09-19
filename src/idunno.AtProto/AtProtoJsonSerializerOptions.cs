// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace idunno.AtProto;

/// <summary>
/// Helper class to provide JsonSerializerOptions for AtProto types.
/// </summary>
// The trimming and AOT suppressions must be applied to the type rather than to s_options. The initializer for a
// static field is emitted into the generated static constructor, and suppression lookup walks from the member the
// IL is attributed to out to its declaring type, so a suppression on the field itself is never consulted by ILLink.
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "All the AtProto Types are captured in source gen")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "All the AtProto Types are captured in source gen")]
public static class AtProtoJsonSerializerOptions
{
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerOptions.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        TypeInfoResolver = SourceGenerationContext.Default,
    };

    /// <summary>
    /// Creates a new set of <see cref="JsonSerializerOptions"/> for AtProto types.
    /// </summary>
    public static JsonSerializerOptions Options => new(s_options);

    /// <summary>
    /// Gets the default <see cref="IJsonTypeInfoResolver"/> for AtProto types.
    /// </summary>
    public static IJsonTypeInfoResolver TypeInfoResolver => s_options.TypeInfoResolver!;
}