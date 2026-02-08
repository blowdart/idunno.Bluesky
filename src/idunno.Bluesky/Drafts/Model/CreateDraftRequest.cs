// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Drafts.Model
{
    internal sealed record CreateDraftRequest
    {
        [SetsRequiredMembers]
        public CreateDraftRequest(Draft draft)
        {
            ArgumentNullException.ThrowIfNull(draft);
            Draft = draft;
        }

        [JsonRequired]
        public required Draft Draft { get; init; }
    }
}
