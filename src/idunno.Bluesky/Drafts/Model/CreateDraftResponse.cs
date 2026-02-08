// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Drafts.Model
{
    internal sealed record CreateDraftResponse
    {
        [JsonConstructor]
        public CreateDraftResponse(string id)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);

            Id = id;
        }

        [JsonRequired]
        public string Id { get; init; }
    }
}
