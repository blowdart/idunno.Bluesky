// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// Represents a detailed error response from an atproto endpoint.
    /// </summary>
    public sealed record AtErrorDetail
    {
        /// <summary>
        /// The title of the error returned from an API call.
        /// </summary>
        [JsonInclude]
        public string? Error { get; internal set; }

        /// <summary>
        /// A human readable explanation specific to this occurrence of the error.
        /// </summary>
        [JsonInclude]
        public string? Message { get; internal set; }

        /// <summary>
        /// The URI returned the error.
        /// </summary>
        [JsonIgnore]
        public Uri? Instance { get; internal set; }

        /// <summary>
        /// The method used when calling the instance.
        /// </summary>
        [JsonIgnore]
        public HttpMethod? HttpMethod { get; internal set; }
    }
}
