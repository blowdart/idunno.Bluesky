// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Drafts.Model
{
    internal sealed record DeleteDraftRequest
    {
        [SetsRequiredMembers]
        public DeleteDraftRequest(TimestampIdentifier id)
        {
            ArgumentNullException.ThrowIfNull(id);
            Id = id;
        }

        [JsonRequired]
        public required TimestampIdentifier Id { get; init; }
    }
}
