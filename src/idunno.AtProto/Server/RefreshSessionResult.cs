// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Server
{
    public sealed record RefreshSessionResult
    {
        public RefreshSessionResult(string accessJwt, string refreshJwt, Handle handle, Did did, DidDocument? didDoc, bool? active, AccountStatus? status)
        {
            AccessJwt = accessJwt;
            RefreshJwt = refreshJwt;
            Handle = handle;
            Did = did;
            DidDoc = didDoc;
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

        public bool? Active {get; init; }

        public AccountStatus? Status { get; init; }
    }
}
