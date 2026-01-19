// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization.Metadata;

namespace idunno.AtProto
{
    /// <summary>
    /// Exposes type resolution for AtProto JSON types.
    /// </summary>
    public static class TypeResolver
    {
        /// <summary>
        /// Gets the default source generation JSON type info resolver for AtProto JSON types.
        /// </summary>
        public static IJsonTypeInfoResolver JsonTypeInfoResolver => SourceGenerationContext.Default;
    }
}
