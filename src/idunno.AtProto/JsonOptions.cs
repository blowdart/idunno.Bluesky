// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
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
            // The JsonSerializerOptions.GetTypeInfo method is called directly and needs a defined resolver
            // setting the default resolver (reflection-based) but the user can overwrite it directly or by modifying
            // the TypeInfoResolverChain. Use JsonTypeInfoResolver.Combine() to produce an empty TypeInfoResolver.
            TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault ? CreateDefaultTypeResolver() : JsonTypeInfoResolver.Combine(SourceGenerationContext.Default)
        };

#pragma warning disable IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
        private static DefaultJsonTypeInfoResolver CreateDefaultTypeResolver() => new ();
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming
    }
}
