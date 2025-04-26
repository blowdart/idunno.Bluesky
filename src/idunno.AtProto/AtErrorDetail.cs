// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// Represents a detailed error response from an atproto endpoint.
    /// </summary>
    [Serializable]
    public sealed class AtErrorDetail
    {
        [JsonConstructor]
        internal AtErrorDetail()
        {
        }
        
        /// <summary>
        /// Creates an new instance of <see cref="AtErrorDetail"/>
        /// </summary>
        /// <param name="error">The error</param>
        /// <param name="message">Any message associated with the error.</param>
        public AtErrorDetail(string? error, string? message)
        {
            Error = error;
            Message = message;
        }

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
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();

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
