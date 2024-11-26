// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Feed.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetRespostedBy.")]
    internal sealed record GetRepostedByResponse
    {
        [JsonConstructor]
        internal GetRepostedByResponse(AtUri uri, Cid cid, ICollection<ProfileView> repostedBy, string? cursor)
        {
            Uri = uri;
            Cid = cid;
            RepostedBy = repostedBy;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public AtUri Uri { get; init; }

        [JsonInclude]
        public Cid? Cid { get; init; }

        [JsonInclude]
        [JsonRequired]
        public ICollection<ProfileView> RepostedBy { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
