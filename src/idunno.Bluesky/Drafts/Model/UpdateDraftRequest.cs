// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Drafts.Model
{
    internal sealed record UpdateDraftRequest
    {
        [SetsRequiredMembers]
        public UpdateDraftRequest(DraftWithId draft)
        {
            ArgumentNullException.ThrowIfNull(draft);
            Draft = draft;
        }

        [JsonRequired]
        [Required]
        public DraftWithId Draft { get; init; }
    }
}
