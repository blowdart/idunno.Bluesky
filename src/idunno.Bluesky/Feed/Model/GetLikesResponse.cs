// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Feed.Model
{
    internal record GetLikesResponse
    {
        [JsonConstructor]
        public GetLikesResponse(AtUri uri, Cid? cid, ICollection<Like> likes, string? cursor)
        {
            Uri = uri;
            Cid = cid;
            Likes = likes;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public AtUri Uri { get; init; }

        [JsonInclude]
        public Cid? Cid { get; init; }

        [JsonInclude]
        [JsonRequired]
        public ICollection<Like> Likes { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
