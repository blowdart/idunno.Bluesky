// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace idunno.Bluesky
{
    /// <summary>
    /// Helper class to provide JsonSerializerOptions for Bluesky types.
    /// </summary>
    public static class BlueskyJsonSerializerOptions
    {
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "All the AtProto Types are captured in source gen")]
        [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "All the AtProto Types are captured in source gen")]
        private static readonly JsonSerializerOptions s_options = new(JsonSerializerOptions.Web)
        {
            TypeInfoResolver = SourceGenerationContext.Default,
        };

        /// <summary>
        /// Creates a new set of <see cref="JsonSerializerOptions"/> for Bluesky types.
        /// </summary>
        public static JsonSerializerOptions Options => new(s_options);

        /// <summary>
        /// Gets the default <see cref="IJsonTypeInfoResolver"/> for Bluesky types.
        /// </summary>
        public static IJsonTypeInfoResolver TypeInfoResolver => s_options.TypeInfoResolver!;
    }
}
