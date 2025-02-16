// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Authentication.Models
{
    /// <summary>
    /// The results of a GetSession API call.
    /// </summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
    public sealed record GetSessionResponse : BaseSessionResponse
    {
        [JsonConstructor]
        internal GetSessionResponse(
            Handle handle,
            Did did,
            string? email,
            bool? emailConfirmed,
            bool? emailAuthFactor,
            DidDocument? didDoc,
            bool? active,
            AccountStatus? status) : base(handle, did, didDoc, active, status)
        {
            Email = email;
            EmailConfirmed = emailConfirmed;
            EmailAuthFactor = emailAuthFactor;
        }

        /// <summary>
        /// The email associated with <see cref="Handle">Handle</see> the newly created session belongs to.
        /// </summary>
        [JsonInclude]
        public string? Email { get; init; }

        /// <summary>
        /// A flag indicating whether the <see cref="Email"/> is confirmed or not.
        /// </summary>
        [JsonInclude]
        public bool? EmailConfirmed { get; init; }

        /// <summary>
        /// A flag indicating whether the newly created session required an email based authentication token.
        /// </summary>
        [JsonInclude]
        public bool? EmailAuthFactor { get; init; }
    }
}
