// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Server
{
    internal record ServiceToken
    {
        public ServiceToken(string token)
        {
            Token = token;
        }

        [JsonInclude]
        [JsonRequired]
        public string Token { get; init; }
    }
}
