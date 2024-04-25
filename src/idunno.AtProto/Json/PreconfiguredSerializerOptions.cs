// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json
{
    /// <summary>
    /// Provides a preconfigured <see cref="JsonSerializerOptions"/> for use when serializing and deserializing atproto requests.
    /// </summary>
    public static class PreconfiguredSerializerOptions
    {
        /// <summary>
        /// Gets the preconfigured <see cref="JsonSerializerOptions"/> for use when serializing and deserializing atproto requests.
        /// </summary>
        public static readonly JsonSerializerOptions Options = new()
        {
            Converters =
            {
                new AtIdentifierConverter(),
                new AtUriJsonConverter(),
                new CIDJsonConverter(),
                new DidJsonConverter(),
                new HandleJsonConverter()
            },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
