// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Drafts.Model
{
    internal sealed record GetDraftsResponse
    {
        [JsonConstructor]
        public GetDraftsResponse(IList<DraftView> drafts, string? cursor)
        {
            ArgumentNullException.ThrowIfNull(drafts);
            Drafts = drafts;
            Cursor = cursor;
        }

        [JsonRequired]
        public IList<DraftView> Drafts { get; init; }

        public string? Cursor { get; init; }
    }
}
