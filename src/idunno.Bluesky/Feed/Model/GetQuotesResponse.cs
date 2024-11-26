// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Feed.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetQuotes.")]
    internal sealed record GetQuotesResponse
    {
        [JsonConstructor]
        public GetQuotesResponse(AtUri uri, Cid? cid, ICollection<PostView> posts, string? cursor)
        {
            Uri = uri;
            Cid = cid;
            Posts = posts;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public AtUri Uri { get; init; }

        [JsonInclude]
        public Cid? Cid { get; init; }

        [JsonInclude]
        [JsonRequired]
        public ICollection<PostView> Posts { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
