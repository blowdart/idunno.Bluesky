// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using idunno.AtProto.Server;

namespace idunno.AtProto.Authentication.Models
{
    /// <summary>
    /// The results of a RefreshSession API call.
    /// </summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
    public sealed record RefreshSessionResponse : BaseSessionResponse
    {
        [JsonConstructor]
        internal RefreshSessionResponse(string accessJwt, string refreshJwt, Handle handle, Did did, DidDocument? didDoc, bool? active, AccountStatus? status)
            : base(handle, did, didDoc, active, status)
        {
            AccessJwt = accessJwt;
            RefreshJwt = refreshJwt;
        }

        /// <summary>
        /// The Access JWT for the newly created session.
        /// </summary>
        [JsonRequired]
        public string AccessJwt { get; init; }

        /// <summary>
        /// The Refresh JWT for the newly created session.
        /// </summary>
        [JsonRequired]
        public string RefreshJwt { get; init; }
    }
}
