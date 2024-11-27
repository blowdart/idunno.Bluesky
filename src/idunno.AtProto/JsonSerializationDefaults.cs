// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using System.Text.Json;

namespace idunno.AtProto
{
    internal static class JsonSerializationDefaults
    {
        /// <summary>
        /// Gets the default <see cref="JsonSerializerOptions"/> to use when deserializing JSON.
        /// </summary>
        public static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            AllowOutOfOrderMetadataProperties = true
        };
    }
}
