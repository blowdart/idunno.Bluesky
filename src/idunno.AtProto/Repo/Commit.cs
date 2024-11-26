// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Metadata about a repo commit.
    /// </summary>
    public sealed record Commit
    {
        [JsonConstructor]
        internal Commit(Cid cid, string rev)
        {
            Cid = cid;
            Rev = rev;
        }

        /// <summary>
        /// The <see cref="AtProto.Cid">Content Identifier</see> of the commit.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Cid Cid { get; init; }

        /// <summary>
        /// The revision of the commit.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Rev { get; init; }

        /// <summary>
        /// Provides a string representation of this instance.
        /// </summary>
        /// <returns>A string representation of this instance.</returns>
        public override string ToString()
        {
            return $"{Cid}/{Rev}";
        }
    }
}
