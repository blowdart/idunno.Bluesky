// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Server
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
    public sealed record CreateSessionResponse
    {
        [JsonConstructor]
        public CreateSessionResponse(
            string accessJwt,
            string refreshJwt,
            Handle handle,
            Did did,
            DidDocument? didDoc = null,
            string? email = null,
            bool? emailConfirmed = null,
            bool? emailAuthFactor = null,
            bool? active = null,
            AccountStatus? status = null)
        {
            AccessJwt = accessJwt;
            RefreshJwt = refreshJwt;
            Handle = handle;
            Did = did;
            DidDoc = didDoc;
            Email = email;
            EmailConfirmed = emailConfirmed;
            EmailAuthFactor = emailAuthFactor;
            Active = active;
            Status = status;
        }

        [JsonRequired]
        public string AccessJwt { get; init; }

        [JsonRequired]
        public string RefreshJwt { get; init; }

        [JsonRequired]
        public Handle Handle { get; init; }

        [JsonRequired]
        public Did Did { get; init; }

        public DidDocument? DidDoc { get; init; }

        public string? Email { get; init; }

        public bool? EmailConfirmed { get; init; }

        public bool? EmailAuthFactor { get; init; }

        public bool? Active { get; init; }

        public AccountStatus? Status { get; init; }
    }
}
