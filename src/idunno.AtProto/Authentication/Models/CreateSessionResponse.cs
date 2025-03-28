// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Authentication.Models
{
    /// <summary>
    /// The results of a CreateSession API call.
    /// </summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
    public sealed record CreateSessionResponse : BaseSessionResponse
    {
        [JsonConstructor]
        internal CreateSessionResponse(
            string accessJwt,
            string refreshJwt,
            Handle handle,
            Did did,
            DidDocument? didDoc = null,
            string? email = null,
            bool? emailConfirmed = null,
            bool? emailAuthFactor = null,
            bool? active = null,
            AccountStatus? status = null) : base(handle, did, didDoc, active, status)
        {
            AccessJwt = accessJwt;
            RefreshJwt = refreshJwt;
            Email = email;
            EmailConfirmed = emailConfirmed;
            EmailAuthFactor = emailAuthFactor;
            Active = active;
            Status = status;
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

        /// <summary>
        /// The email associated with <see cref="Handle">Handle</see> the newly created session belongs to.
        /// </summary>
        public string? Email { get; init; }

        /// <summary>
        /// A flag indicating whether the <see cref="Email"/> is confirmed or not.
        /// </summary>
        public bool? EmailConfirmed { get; init; }

        /// <summary>
        /// A flag indicating whether the newly created session required an email based authentication token.
        /// </summary>
        public bool? EmailAuthFactor { get; init; }
    }
}
