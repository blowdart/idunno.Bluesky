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
// The trimming and AOT suppressions are applied to the type so that they cover every member which reaches the
// chaining call, wherever the compiler emits that IL. Suppression lookup walks from the member the IL is
// attributed to out to its declaring type, so a type level suppression is always consulted by ILLink.
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "All the AtProto Types are captured in source gen")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "All the AtProto Types are captured in source gen")]
public static class BlueskyJsonSerializerOptions
{
    /// <summary>
    /// Creates a new set of <see cref="JsonSerializerOptions"/> for Bluesky types.
    /// </summary>
    public static JsonSerializerOptions Options
    {
        get
        {
            // The chaining call builds a fresh set of AtProto options and inserts the resolver it is given, so
            // nothing else can be configured on the way in. Everything Bluesky needs beyond the AtProto defaults,
            // converters included, has to be applied to the result.
            JsonSerializerOptions options = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(SourceGenerationContext.Default);

            options.Converters.Add(new Json.PreferenceConverter());

            return options;
        }
    }

    /// <summary>
    /// Gets the default <see cref="IJsonTypeInfoResolver"/> for Bluesky types.
    /// </summary>
    public static IJsonTypeInfoResolver TypeInfoResolver => Options.TypeInfoResolver!;
}