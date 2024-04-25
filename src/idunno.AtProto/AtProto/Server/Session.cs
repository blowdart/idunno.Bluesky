// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Server
{
    public sealed record Session
    {
        [JsonConstructor]
        public Session(Did did, Handle handle, string accessJwt, string refreshJwt)
        {
            Did = did;
            Handle = handle;
            AccessJwt = accessJwt;
            RefreshJwt = refreshJwt;
        }

        [JsonInclude]
        [JsonRequired]
        public string AccessJwt { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public string RefreshJwt { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public Did Did { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public Handle Handle { get; internal set; }

        [JsonInclude]
        [JsonPropertyName("didDoc")]
        public DidDocument? DidDocument { get; internal set; }

        [JsonInclude]
        public string? Email { get; internal set; }

        [JsonInclude]
        public bool EmailConfirmed { get; internal set; }

        [JsonIgnore]
        public Uri? Service { get; internal set; }
    }
}
