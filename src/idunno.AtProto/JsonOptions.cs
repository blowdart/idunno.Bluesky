// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace idunno.AtProto
{
    /// <summary>
    /// Options to configure JSON serialization settings for <see cref="Agent"/>
    /// </summary>
    public sealed class JsonOptions
    {
        /// <summary>
        /// Gets the <see cref="JsonSerializerOptions"/> used by <see cref="Agent"/> 
        /// </summary>
        public JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IgnoreReadOnlyProperties = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,

            // The JsonSerializerOptions.GetTypeInfo method is called directly and needs a defined resolver
            // setting the default resolver (reflection-based) but the user can overwrite it directly or by modifying
            // the TypeInfoResolverChain. Use JsonTypeInfoResolver.Combine() to produce an empty TypeInfoResolver.
            TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault ? CreateDefaultTypeResolver() : JsonTypeInfoResolver.Combine(SourceGenerationContext.Default)
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
}
