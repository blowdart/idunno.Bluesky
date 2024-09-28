// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Server
{
    public sealed record GetSessionResult
    {
        public GetSessionResult(
            Handle handle,
            Did did,
            string? email,
            bool? emailConfirmed,
            bool? emailAuthFactor,
            DidDocument? didDoc,
            bool? active,
            AccountStatus? status)
        {
            Handle = handle;
            Did = did;
            Email = email;
            EmailConfirmed = emailConfirmed;
            EmailAuthFactor = emailAuthFactor;
            DidDoc = didDoc;
            Active = active;
            Status = status;
        }

        [JsonRequired]
        public Handle Handle { get; init; }

        [JsonRequired]
        public Did Did { get; init; }

        public string? Email { get; init; }

        public bool? EmailConfirmed { get; init; } 

        public bool? EmailAuthFactor { get; init; }

        public DidDocument? DidDoc { get; init; }

        public bool? Active {get; init; }

        public AccountStatus? Status { get; init; }
    }
}
