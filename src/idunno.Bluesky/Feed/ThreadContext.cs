// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Encapsulates metadata about this post within the context of the thread it is in.
    /// </summary>
    public sealed record ThreadContext : AtProtoObject
    {
        /// <summary>
        /// Creates a new instance of <see cref="ThreadContext"/>.
        /// </summary>
        /// <param name="rootAuthorLike">The <see cref="AtUri"/> of a like on a <see cref="ThreadViewPost"/> from the author of the threat root, if any</param>
        [JsonConstructor]
        public ThreadContext(AtUri? rootAuthorLike)
        {
            RootAuthorLike = rootAuthorLike;
        }

        /// <summary>
        /// The <see cref="AtUri"/> of a like on a <see cref="ThreadViewPost"/> from the author of the threat root, if any.
        /// </summary>
        [JsonInclude]
        public AtUri? RootAuthorLike { get; init; }
    }
}
